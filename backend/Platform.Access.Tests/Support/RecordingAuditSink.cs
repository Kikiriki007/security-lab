using Platform.Access.Infrastructure.Auditing;

namespace Platform.Access.Tests.Support;

internal sealed class RecordingAuditSink : IAccessAuditSink
{
    public List<AccessAuditRecord> Records { get; } = [];

    public ValueTask WriteAsync(AccessAuditRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return ValueTask.CompletedTask;
    }
}
