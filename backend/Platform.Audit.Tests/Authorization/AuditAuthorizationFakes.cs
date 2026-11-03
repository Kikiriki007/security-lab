using System.Collections.Concurrent;
using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Audit.Contracts;
using Platform.Identity.Contracts;
using Platform.Identity.Domain.Enums;

namespace Platform.Audit.Tests.Authorization;

// Test-only providers: they never become registrations in the real API host.
internal sealed class StubAuditIdentity : IIdentityContext
{
    private readonly ConcurrentDictionary<Guid, AccountSnapshot> accounts = new();
    private int calls;

    public int Calls => Volatile.Read(ref calls);
    public Func<Guid, CancellationToken, ValueTask<AccountSnapshot?>>? Handler { get; set; }

    public void Set(Guid accountId, AccountStatus status) =>
        accounts[accountId] = new AccountSnapshot(accountId, status);

    public ValueTask<AccountSnapshot?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref calls);
        cancellationToken.ThrowIfCancellationRequested();
        if (Handler is not null)
            return Handler(accountId, cancellationToken);

        return ValueTask.FromResult(accounts.TryGetValue(accountId, out var account) ? account : null);
    }
}

internal sealed class StubCourseAuditMemberships : ICourseAuditMembershipReader
{
    private readonly ConcurrentDictionary<(Guid AccountId, Guid CourseId), CourseAuditMembership> memberships = new();
    private int calls;

    public int Calls => Volatile.Read(ref calls);
    public Func<Guid, Guid, CancellationToken, ValueTask<CourseAuditMembership?>>? Handler { get; set; }

    public void Set(CourseAuditMembership membership) =>
        memberships[(membership.AccountId, membership.CourseId)] = membership;

    public ValueTask<CourseAuditMembership?> FindAsync(
        Guid accountId, Guid courseId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref calls);
        cancellationToken.ThrowIfCancellationRequested();
        if (Handler is not null)
            return Handler(accountId, courseId, cancellationToken);

        return ValueTask.FromResult(memberships.TryGetValue((accountId, courseId), out var membership) ? membership : null);
    }
}

internal sealed class AuditAuthorizationClock : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal static class AuditAuthorizationExamples
{
    public static readonly Guid TeacherId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid OtherAccountId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid CourseK = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    public static readonly Guid CourseL = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    public static AccessRequest Request(Guid? accountId = null, Guid? courseId = null, string correlationId = "audit-test") =>
        new(new SubjectRef((accountId ?? TeacherId).ToString(), AccountState.Active),
            "audit.events.read", new ResourceRef("Course", (courseId ?? CourseK).ToString()), correlationId);

    public static CourseAuditMembership Membership(
        CourseAuditRole role = CourseAuditRole.Teacher,
        Guid? accountId = null,
        Guid? courseId = null,
        params CourseAuditDelegation[] delegations) =>
        new(accountId ?? TeacherId, courseId ?? CourseK, role, true, delegations);

    public static CourseAuditDelegation Delegation(DateTimeOffset now) =>
        new(TeacherId, CourseK, "audit.events.read", now.AddHours(-1), now.AddHours(1));
}

internal sealed class RecordingAccessAuditWriter : IAccessAuditWriter
{
    public List<(AccessRequest Request, AccessDecision Decision)> Entries { get; } = new();

    public ValueTask WriteAsync(AccessRequest request, AccessDecision decision, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entries.Add((request, decision));
        return ValueTask.CompletedTask;
    }
}
