using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Closes expired denial windows on a timer and writes every held-back count at shutdown, so the summary of the
/// last window of a burst is not lost until another denial arrives (or forever, if the process stops).
/// A failing flush is logged as a technical alarm (reason, phase and exception type only, never an exception
/// message or record content) and never stops the loop or the host: the failed summaries stay in the sink and
/// are retried by the next flush.
/// </summary>
internal sealed partial class AccessAuditFlushService(
    AggregatingAccessAuditSink sink,
    AccessAuditOptions options,
    TimeProvider clock,
    ILogger<AccessAuditFlushService> logger,
    AccessAuditMetrics metrics) : BackgroundService
{
    private const string PeriodicPhase = "periodic";
    private const string ShutdownPhase = "shutdown";
    private const string FlushFailedReason = "flush_failed";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.FlushInterval, clock);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await FlushAsync(sink.FlushExpiredAsync, PeriodicPhase, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal stop; the shutdown flush runs in StopAsync.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await FlushAsync(sink.FlushAllAsync, ShutdownPhase, cancellationToken).ConfigureAwait(false);
    }

    private async Task FlushAsync(
        Func<CancellationToken, ValueTask> flush,
        string phase,
        CancellationToken cancellationToken)
    {
        try
        {
            await flush(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopping; whatever is still held is written by the shutdown flush.
        }
        catch (Exception ex)
        {
            RaiseAlarm(phase, ex.GetType().Name);
        }
    }

    private void RaiseAlarm(string phase, string exceptionType)
    {
        metrics.RecordFailure(FlushFailedReason);

        try
        {
            LogFlushFailure(logger, phase, exceptionType);
        }
        catch (Exception)
        {
            // The alarm itself must never stop the flush loop or the shutdown.
        }
    }

    [LoggerMessage(
        EventId = 6104,
        EventName = "access.audit.flush.failure",
        Level = LogLevel.Error,
        Message = "access.audit.flush.failure phase={Phase} exceptionType={ExceptionType}")]
    private static partial void LogFlushFailure(ILogger logger, string phase, string exceptionType);
}
