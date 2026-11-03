using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Audit.Contracts;
using Platform.Identity.Contracts;
using Platform.Identity.Domain.Enums;

namespace Platform.Audit.Application.Authorization;

/// <summary>
/// S-08: course-scoped authorization for AUD-06. Tested through the existing Access
/// contract; host registration waits for the Team 3 composition/lifetime agreement.
/// The request subject must have been resolved by a trusted server adapter, not a body/header.
/// </summary>
internal sealed class CourseAuditReadPolicy(
    IIdentityContext identity,
    ICourseAuditMembershipReader memberships,
    TimeProvider clock) : IAccessPolicy
{
    public const string ActionId = "audit.events.read";

    public string Id => "audit.course.read";
    public string Version => "v1";

    public async ValueTask<bool> EvaluateAsync(
        AccessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request is null
            || request.Action != ActionId
            || request.Subject is not { State: AccountState.Active } subject
            || !Guid.TryParse(subject.Id, out var accountId)
            || accountId == Guid.Empty
            || request.Resource is not { Type: AuditResourceTypes.Course } resource
            || !Guid.TryParse(resource.Id, out var courseId)
            || courseId == Guid.Empty)
        {
            return false;
        }

        try
        {
            // Look up current account state even if the request still says Active.
            // IIdentityContext here is the existing account lookup, not HTTP authentication.
            var account = await identity.FindByIdAsync(accountId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (account is not { Status: AccountStatus.Active } || account.Id != accountId)
                return false;

            // Fetch on every decision: a revoked membership/grant cannot survive in a policy cache.
            var membership = await memberships.FindAsync(accountId, courseId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (membership is not { IsActive: true }
                || membership.AccountId != accountId
                || membership.CourseId != courseId)
            {
                return false;
            }

            if (membership.Role == CourseAuditRole.Teacher)
                return true;

            if (membership.Role != CourseAuditRole.Assistant || membership.Delegations is null)
                return false;

            var now = clock.GetUtcNow();
            return membership.Delegations.Any(grant =>
                grant is not null
                && grant.AccountId == accountId
                && grant.CourseId == courseId
                && grant.Action == ActionId
                && grant.RevokedAt is null
                && grant.ValidFrom <= now
                && now < grant.ExpiresAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Unavailable or malformed provider data never grants access. No rejected
            // payload, identifier or exception text is copied to an HTTP response/log.
            return false;
        }
    }
}
