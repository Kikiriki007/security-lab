namespace Platform.Audit.Contracts;

/// <summary>
/// Provisional S-08 boundary, pending agreement with the Membership/Delegation owners.
/// Reads current server-side rights for one account in one course; never trusts client roles.
/// An unknown or inactive membership must not be replaced by a global role.
/// </summary>
public interface ICourseAuditMembershipReader
{
    ValueTask<CourseAuditMembership?> FindAsync(
        Guid accountId,
        Guid courseId,
        CancellationToken cancellationToken = default);
}
