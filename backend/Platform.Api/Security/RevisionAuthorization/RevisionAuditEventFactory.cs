using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Audit.Contracts;

namespace Platform.Api.Security.RevisionAuthorization;

public static class RevisionAuditEventFactory
{
    public static AuditEventDraft? CreateDeniedEvent(
        AccessRequest request,
        AccessDecision decision)
    {
        if (decision.Outcome != AccessOutcome.Deny)
        {
            return null;
        }

        if (request.Action is not (
            RevisionActions.Submit or
            RevisionActions.Approve or
            RevisionActions.Return or
            RevisionActions.UnlockAccount))
        {
            return null;
        }

        var details = new Dictionary<string, string>
        {
            ["action"] = request.Action,
            ["denialReason"] = ToReasonCode(decision.DenialReason)
        };

        if (!string.IsNullOrWhiteSpace(decision.PolicyId))
        {
            details["policyId"] = decision.PolicyId;
        }

        if (!string.IsNullOrWhiteSpace(decision.PolicyVersion) &&
            System.Text.RegularExpressions.Regex.IsMatch(
                decision.PolicyVersion, @"^v[0-9]{1,4}(\.[0-9]{1,4}){0,2}$"))
        {
            details["policyVersion"] = decision.PolicyVersion;
        }

        return new AuditEventDraft(
            AuditEventTypes.AccessDenied,
            AuditOutcome.Denied,
            Details: details);
    }

    private static string ToReasonCode(AccessDenialReason reason) => reason switch
    {
        AccessDenialReason.Unauthenticated => "unauthenticated",
        AccessDenialReason.AccountNotActive => "account_not_active",
        AccessDenialReason.UnknownAction => "unknown_action",
        AccessDenialReason.NoPolicy => "no_policy",
        AccessDenialReason.PolicyError => "policy_error",
        _ => "policy_error"
    };
}

