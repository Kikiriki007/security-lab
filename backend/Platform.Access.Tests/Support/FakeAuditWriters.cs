using Platform.Access.Contracts;
using Platform.Access.Domain.Models;

namespace Platform.Access.Tests.Support;

/// <summary>Writer that throws before returning a task.</summary>
internal sealed class SyncThrowingWriter(Exception exception) : IAccessAuditWriter
{
    public ValueTask WriteAsync(AccessRequest request, AccessDecision decision, CancellationToken cancellationToken = default) =>
        throw exception;
}

/// <summary>Writer that throws asynchronously, after the first await.</summary>
internal sealed class AsyncThrowingWriter(Exception exception) : IAccessAuditWriter
{
    public async ValueTask WriteAsync(AccessRequest request, AccessDecision decision, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        throw exception;
    }
}

/// <summary>Writer that never finishes and ignores its token, like a hung network call.</summary>
internal sealed class HangingWriter : IAccessAuditWriter
{
    public ValueTask WriteAsync(AccessRequest request, AccessDecision decision, CancellationToken cancellationToken = default) =>
        new(new TaskCompletionSource().Task);
}

/// <summary>Writer that waits on its token and reports when the token was cancelled.</summary>
internal sealed class TokenObservingWriter : IAccessAuditWriter
{
    private readonly TaskCompletionSource<bool> _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the writer saw its token cancelled.</summary>
    public Task Cancelled => _cancelled.Task;

    public async ValueTask WriteAsync(AccessRequest request, AccessDecision decision, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _cancelled.TrySetResult(true);
            throw;
        }
    }
}

/// <summary>Writer that records the token it was given and finishes at once.</summary>
internal sealed class TokenRecordingWriter : IAccessAuditWriter
{
    public bool Called { get; private set; }

    public bool TokenWasCancelled { get; private set; }

    public ValueTask WriteAsync(AccessRequest request, AccessDecision decision, CancellationToken cancellationToken = default)
    {
        Called = true;
        TokenWasCancelled = cancellationToken.IsCancellationRequested;
        return ValueTask.CompletedTask;
    }
}
