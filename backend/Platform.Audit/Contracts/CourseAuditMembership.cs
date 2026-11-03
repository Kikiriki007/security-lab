namespace Platform.Audit.Contracts;

/// <summary>
/// Provisional current membership snapshot for S-08. Identifiers are included so the
/// policy can reject a provider result for a different account or course.
/// </summary>
public sealed record CourseAuditMembership(
    Guid AccountId,
    Guid CourseId,
    CourseAuditRole Role,
    bool IsActive,
    IReadOnlyList<CourseAuditDelegation> Delegations);

public enum CourseAuditRole
{
    Student,
    Teacher,
    Assistant
}

/// <summary>
/// Provisional grant for exactly one account, course and action. The interval is
/// [ValidFrom, ExpiresAt); any RevokedAt value makes the grant unusable.
/// </summary>
public sealed record CourseAuditDelegation(
    Guid AccountId,
    Guid CourseId,
    string Action,
    DateTimeOffset ValidFrom,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt = null);
