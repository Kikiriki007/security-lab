using System.Text.RegularExpressions;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Access.Domain.ValueObjects;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Builds an <see cref="AccessAuditRecord"/> from a decision. Every value is taken from the
/// server-side request and decision, sanitized and bounded. A value that does not match its
/// expected format is recorded as <see cref="AccessAuditRecord.Invalid"/>, never as raw input.
/// </summary>
internal sealed partial class AccessAuditRecordFactory(TimeProvider clock, AccessAuditOptions options)
{
    // Formats from the Audit module detail catalog (docs/components/audit/CONTRACTS.md).
    [GeneratedRegex(@"^[a-z][a-z0-9_.-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex PolicyIdPattern();

    [GeneratedRegex(@"^v[0-9]{1,4}(\.[0-9]{1,4}){0,2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex PolicyVersionPattern();

    public AccessAuditRecord Create(AccessRequest request, AccessDecision decision)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(decision);

        var denied = decision.Outcome != AccessOutcome.Allow;

        return new AccessAuditRecord(
            Time: clock.GetUtcNow(),
            EventType: denied ? AccessAuditRecord.DeniedEventType : AccessAuditRecord.PrivilegedAllowedEventType,
            Actor: ActorOf(request.Subject),
            Action: ActionOf(request.Action),
            Target: TargetOf(request.Resource),
            Outcome: denied ? "deny" : "allow",
            CorrelationId: OrNoValue(Clean(request.CorrelationId)),
            DenialReason: denied ? DenialReasonOf(decision.DenialReason) : null,
            PolicyId: decision.PolicyId is null ? null : Matching(decision.PolicyId, PolicyIdPattern()),
            PolicyVersion: Matching(decision.PolicyVersion, PolicyVersionPattern()));
    }

    /// <summary>
    /// Snake-case values accepted by the Audit module for <c>denialReason</c>.
    /// An unknown value is recorded as <c>policy_error</c> so a new enum member can never
    /// produce a value outside the agreed set.
    /// </summary>
    internal static string DenialReasonOf(AccessDenialReason reason) => reason switch
    {
        AccessDenialReason.Unauthenticated => "unauthenticated",
        AccessDenialReason.AccountNotActive => "account_not_active",
        AccessDenialReason.UnknownAction => "unknown_action",
        AccessDenialReason.NoPolicy => "no_policy",
        _ => "policy_error",
    };

    private string ActorOf(SubjectRef? subject)
    {
        if (subject is null)
        {
            return AccessAuditRecord.Anonymous;
        }

        var id = Clean(subject.Id);
        return id.Length == 0 ? AccessAuditRecord.Invalid : id;
    }

    // The raw action is checked before any cleaning: a value that needed cleaning is not a valid action.
    private static string ActionOf(string? action) =>
        action is not null && ActionId.IsValid(action) && !action.Any(char.IsControl)
            ? action
            : AccessAuditRecord.Invalid;

    private string TargetOf(ResourceRef? resource)
    {
        if (resource is null)
        {
            return AccessAuditRecord.NoValue;
        }

        var type = OrNoValue(Clean(resource.Type));
        var id = OrNoValue(Clean(resource.Id));

        return AuditValueSanitizer.Clean($"{type}:{id}", options.MaxValueLength);
    }

    private string Matching(string? value, Regex pattern) =>
        value is not null && value.Length <= options.MaxValueLength && pattern.IsMatch(value)
            ? value
            : AccessAuditRecord.Invalid;

    private string Clean(string? value) => AuditValueSanitizer.Clean(value, options.MaxValueLength);

    private static string OrNoValue(string value) => value.Length == 0 ? AccessAuditRecord.NoValue : value;
}
