using Platform.Access.Infrastructure.Auditing;

namespace Platform.Access.Tests.Support;

internal sealed class RecordingAuditSink : IAccessAuditSink
{
    private readonly List<AccessAuditRecord> _records = [];

    /// <summary>Snapshot of what was written so far; safe to read while other threads write.</summary>
    public IReadOnlyList<AccessAuditRecord> Records
    {
        get
        {
            lock (_records)
            {
                return [.. _records];
            }
        }
    }

    public CancellationToken LastToken { get; private set; }

    public ValueTask WriteAsync(AccessAuditRecord record, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            _records.Add(record);
            LastToken = cancellationToken;
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Sink that fails a chosen number of times, then records normally.</summary>
internal sealed class FailingThenRecordingSink(int failures) : IAccessAuditSink
{
    private int _remaining = failures;

    public RecordingAuditSink Inner { get; } = new();

    public ValueTask WriteAsync(AccessAuditRecord record, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref _remaining) >= 0)
        {
            throw new InvalidOperationException("sink failed");
        }

        return Inner.WriteAsync(record, cancellationToken);
    }
}
