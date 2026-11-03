using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Api.Domain.Identity;
using PlatformAccessDecision = Platform.Access.Contracts.IAccessDecision;

namespace Platform.Api.Services.Identity;

/// <summary>
/// Bridges the IDN-03 use-case (<see cref="IAccessDecision"/>, synchronous, takes a <see cref="CallerIdentity"/>)
/// to the central default-deny engine (<c>Platform.Access.Contracts.IAccessDecision</c>). The decision, the
/// reason and the audit of every denied or privileged access now come from one place.
/// Temporary: once <c>ChangeAccountStatus</c> is asynchronous it can call the engine directly and this
/// adapter can be deleted.
/// </summary>
/// <remarks>
/// Any failure means "no": an unreadable account, an engine fault or a cancelled call returns false.
/// Blocking on the engine is safe here, the host has no synchronization context.
/// </remarks>
public sealed class PlatformAccessDecisionAdapter(
    PlatformAccessDecision engine,
    IAccountRepository accounts,
    IHttpContextAccessor http) : IAccessDecision
{
    public bool CanChangeAccountStatus(CallerIdentity caller)
    {
        try
        {
            var request = new AccessRequest(
                SubjectOf(caller),
                AccountStatusChangePolicy.ActionId,
                null,
                CorrelationId());

            var pending = engine.DecideAsync(request);
            var decision = pending.IsCompletedSuccessfully
                ? pending.Result
                : pending.AsTask().GetAwaiter().GetResult();

            return decision.Outcome == AccessOutcome.Allow;
        }
        catch (Exception)
        {
            // Fail closed: the engine already audits denials; an unexpected fault must never allow.
            return false;
        }
    }

    // An unauthenticated caller (including a pending second factor) has no subject, so the engine
    // answers Unauthenticated. An account that cannot be found is not active.
    private SubjectRef? SubjectOf(CallerIdentity caller)
    {
        if (!caller.IsAuthenticated || caller.UserId is not { } userId || userId == Guid.Empty)
        {
            return null;
        }

        var state = accounts.Find(userId)?.Status switch
        {
            AccountStatus.Active => AccountState.Active,
            AccountStatus.Archived => AccountState.Archived,
            _ => AccountState.Blocked,
        };

        return new SubjectRef(userId.ToString("D"), state);
    }

    // The use-case writes its own audit line with HttpContext.TraceIdentifier; using the same id lets
    // both records be matched.
    private string CorrelationId()
    {
        var traceId = http.HttpContext?.TraceIdentifier;
        return string.IsNullOrEmpty(traceId) ? Guid.NewGuid().ToString("N") : traceId;
    }
}
