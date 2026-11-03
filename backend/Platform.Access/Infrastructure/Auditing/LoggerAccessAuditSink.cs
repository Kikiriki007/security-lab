using Microsoft.Extensions.Logging;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Writes an access audit record as one structured log entry. Only the whitelisted record fields
/// are passed to the logger, so nothing else can reach the log through this sink.
/// A deny is logged as a warning, an allowed privileged action as information.
/// </summary>
internal sealed partial class LoggerAccessAuditSink(ILogger<LoggerAccessAuditSink> logger) : IAccessAuditSink
{
    public ValueTask WriteAsync(AccessAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        // Anything that is not a privileged allow is treated as a denial: a record is never dropped silently.
        if (record.EventType == AccessAuditRecord.PrivilegedAllowedEventType)
        {
            LogPrivilegedAllowed(
                logger, record.Time, record.Actor, record.Action, record.Target, record.Outcome,
                record.CorrelationId, record.DenialReason, record.PolicyId, record.PolicyVersion, record.Count);
        }
        else
        {
            LogDenied(
                logger, record.Time, record.Actor, record.Action, record.Target, record.Outcome,
                record.CorrelationId, record.DenialReason, record.PolicyId, record.PolicyVersion, record.Count);
        }

        return ValueTask.CompletedTask;
    }

    [LoggerMessage(
        EventId = 6101,
        EventName = AccessAuditRecord.DeniedEventType,
        Level = LogLevel.Warning,
        Message = "access.denied time={Time:O} actor={Actor} action={Action} target={Target} outcome={Outcome} " +
                  "correlationId={CorrelationId} denialReason={DenialReason} policyId={PolicyId} " +
                  "policyVersion={PolicyVersion} count={Count}")]
    private static partial void LogDenied(
        ILogger logger, DateTimeOffset time, string actor, string action, string target, string outcome,
        string correlationId, string? denialReason, string? policyId, string? policyVersion, int count);

    [LoggerMessage(
        EventId = 6102,
        EventName = AccessAuditRecord.PrivilegedAllowedEventType,
        Level = LogLevel.Information,
        Message = "access.allowed.privileged time={Time:O} actor={Actor} action={Action} target={Target} outcome={Outcome} " +
                  "correlationId={CorrelationId} denialReason={DenialReason} policyId={PolicyId} " +
                  "policyVersion={PolicyVersion} count={Count}")]
    private static partial void LogPrivilegedAllowed(
        ILogger logger, DateTimeOffset time, string actor, string action, string target, string outcome,
        string correlationId, string? denialReason, string? policyId, string? policyVersion, int count);
}
