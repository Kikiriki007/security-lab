using Microsoft.Extensions.Logging;
using Platform.Access.Contracts;
using Platform.Access.Domain.Models;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Outermost audit decorator. A failing or slow audit write must never change an access decision
/// (Analiza_zadatka 8.1 #5), so this writer:
/// <list type="bullet">
/// <item>catches every exception, including one thrown before a task is returned;</item>
/// <item>bounds the write with its own timeout (<see cref="AccessAuditOptions.WriteTimeout"/>), independent
/// of the request token, and cancels the inner write when the timeout passes;</item>
/// <item>raises a technical alarm (error log) that carries no secrets: only the reason, the exception
/// type name and the sanitized correlation id, never an exception message, stack or record content.</item>
/// </list>
/// It never throws and never returns a value, so it cannot turn a deny into an allow or the opposite.
/// </summary>
internal sealed partial class ResilientAccessAuditWriter(
    IAccessAuditWriter inner,
    AccessAuditOptions options,
    TimeProvider clock,
    ILogger<ResilientAccessAuditWriter> logger) : IAccessAuditWriter
{
    private const string FailedReason = "failed";
    private const string TimedOutReason = "timed_out";

    // The caller's token is ignored on purpose: a cancelled request must still be audited.
    public async ValueTask WriteAsync(
        AccessRequest request,
        AccessDecision decision,
        CancellationToken cancellationToken = default)
    {
        using var cts = new CancellationTokenSource(options.WriteTimeout, clock);
        Task? write = null;

        try
        {
            write = inner.WriteAsync(request, decision, cts.Token).AsTask();
            await write.WaitAsync(options.WriteTimeout, clock).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Stop(cts, write);
            RaiseAlarm(request, TimedOutReason, null);
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            // The inner writer honoured our own timeout token.
            RaiseAlarm(request, TimedOutReason, ex.GetType().Name);
        }
        catch (Exception ex)
        {
            RaiseAlarm(request, FailedReason, ex.GetType().Name);
        }
    }

    // After a timeout the inner write may still run; cancel it and observe a later fault so it
    // cannot surface as an unobserved task exception.
    private static void Stop(CancellationTokenSource cts, Task? write)
    {
        try
        {
            cts.Cancel();
        }
        catch (Exception)
        {
            // A cancellation callback of the inner writer failed; nothing more to do here.
        }

        write?.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void RaiseAlarm(AccessRequest? request, string reason, string? exceptionType)
    {
        try
        {
            var correlationId = AuditValueSanitizer.Clean(request?.CorrelationId, options.MaxValueLength);
            var type = AuditValueSanitizer.Clean(exceptionType, options.MaxValueLength);

            LogAuditFailure(
                logger,
                reason,
                type.Length == 0 ? AccessAuditRecord.NoValue : type,
                correlationId.Length == 0 ? AccessAuditRecord.NoValue : correlationId);
        }
        catch (Exception)
        {
            // The alarm itself must never change an access decision.
        }
    }

    [LoggerMessage(
        EventId = 6103,
        EventName = "access.audit.failure",
        Level = LogLevel.Error,
        Message = "access.audit.failure reason={Reason} exceptionType={ExceptionType} correlationId={CorrelationId}")]
    private static partial void LogAuditFailure(
        ILogger logger, string reason, string exceptionType, string correlationId);
}
