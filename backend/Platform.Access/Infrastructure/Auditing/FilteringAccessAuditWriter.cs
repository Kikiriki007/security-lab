using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Decides what is audited: every denial, and an allow only for privileged actions
/// (Analiza_zadatka 9.1). Everything else is dropped here, before a record is built.
/// </summary>
internal sealed class FilteringAccessAuditWriter(
    AccessAuditRecordFactory factory,
    IAccessAuditSink sink,
    AccessAuditOptions options) : IAccessAuditWriter
{
    public ValueTask WriteAsync(
        AccessRequest request,
        AccessDecision decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(decision);

        return ShouldAudit(request, decision)
            ? sink.WriteAsync(factory.Create(request, decision), cancellationToken)
            : ValueTask.CompletedTask;
    }

    // Only an explicit Allow can be skipped; any other outcome is audited as a denial.
    private bool ShouldAudit(AccessRequest request, AccessDecision decision) =>
        decision.Outcome != AccessOutcome.Allow || options.PrivilegedActions.Contains(request.Action);
}
