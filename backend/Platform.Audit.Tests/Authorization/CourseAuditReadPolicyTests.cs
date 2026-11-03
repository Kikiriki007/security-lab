using NUnit.Framework;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Audit.Application.Authorization;
using Platform.Audit.Contracts;
using Platform.Identity.Contracts;
using Platform.Identity.Domain.Enums;
using static Platform.Audit.Tests.Authorization.AuditAuthorizationExamples;

namespace Platform.Audit.Tests.Authorization;

[TestFixture]
public sealed class CourseAuditReadPolicyTests
{
    private StubAuditIdentity identity = null!;
    private StubCourseAuditMemberships memberships = null!;
    private AuditAuthorizationClock clock = null!;
    private CourseAuditReadPolicy policy = null!;

    [SetUp]
    public void SetUp()
    {
        identity = new StubAuditIdentity();
        identity.Set(TeacherId, AccountStatus.Active);
        memberships = new StubCourseAuditMemberships();
        clock = new AuditAuthorizationClock();
        policy = new CourseAuditReadPolicy(identity, memberships, clock);
    }

    [Test]
    public async Task ActiveTeacher_SeesTheirCourse()
    {
        memberships.Set(Membership());

        Assert.That(await policy.EvaluateAsync(Request()), Is.True);
    }

    [Test]
    public async Task TeacherInK_StudentInL_DoesNotInheritTeacherPermissionInL()
    {
        memberships.Set(Membership());
        memberships.Set(Membership(CourseAuditRole.Student, courseId: CourseL));

        Assert.That(await policy.EvaluateAsync(Request(courseId: CourseK)), Is.True);
        Assert.That(await policy.EvaluateAsync(Request(courseId: CourseL)), Is.False);
    }

    [Test]
    public async Task ActiveAssistant_WithExactCourseActionDelegation_IsAllowed()
    {
        memberships.Set(Membership(CourseAuditRole.Assistant, delegations: [Delegation(clock.UtcNow)]));

        Assert.That(await policy.EvaluateAsync(Request()), Is.True);
    }

    [Test]
    public async Task Delegation_IsValidAtItsStart()
    {
        memberships.Set(Membership(CourseAuditRole.Assistant,
            delegations: [Delegation(clock.UtcNow) with { ValidFrom = clock.UtcNow }]));

        Assert.That(await policy.EvaluateAsync(Request()), Is.True);
    }

    [TestCase(CourseAuditRole.Student)]
    [TestCase((CourseAuditRole)999)]
    public async Task NonTeachingRole_IsDeniedEvenWithADelegation(CourseAuditRole role)
    {
        memberships.Set(Membership(role, delegations: [Delegation(clock.UtcNow)]));

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [Test]
    public async Task AssistantWithoutDelegation_IsDenied()
    {
        memberships.Set(Membership(CourseAuditRole.Assistant));

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [TestCase("other-account")]
    [TestCase("other-course")]
    [TestCase("other-action")]
    [TestCase("action-case")]
    [TestCase("future")]
    [TestCase("expires-now")]
    [TestCase("expired")]
    [TestCase("revoked")]
    [TestCase("revoked-in-future")]
    [TestCase("empty-interval")]
    [TestCase("reversed-interval")]
    public async Task InvalidOrInapplicableDelegation_DoesNotGrantAccess(string scenario)
    {
        var delegation = Delegation(clock.UtcNow);
        delegation = scenario switch
        {
            "other-account" => delegation with { AccountId = OtherAccountId },
            "other-course" => delegation with { CourseId = CourseL },
            "other-action" => delegation with { Action = "attempts.read" },
            "action-case" => delegation with { Action = "Audit.Events.Read" },
            "future" => delegation with { ValidFrom = clock.UtcNow.AddMinutes(1) },
            "expires-now" => delegation with { ExpiresAt = clock.UtcNow },
            "expired" => delegation with { ExpiresAt = clock.UtcNow.AddMinutes(-1) },
            "revoked" => delegation with { RevokedAt = clock.UtcNow.AddMinutes(-1) },
            "revoked-in-future" => delegation with { RevokedAt = clock.UtcNow.AddMinutes(1) },
            "empty-interval" => delegation with { ValidFrom = clock.UtcNow, ExpiresAt = clock.UtcNow },
            "reversed-interval" => delegation with { ValidFrom = clock.UtcNow.AddHours(1), ExpiresAt = clock.UtcNow.AddHours(-1) },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        memberships.Set(Membership(CourseAuditRole.Assistant, delegations: [delegation]));

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [Test]
    public async Task ValidDelegationAmongUnrelatedDelegations_GrantsOnlyItsOwnCourse()
    {
        memberships.Set(Membership(CourseAuditRole.Assistant, delegations:
        [
            Delegation(clock.UtcNow) with { Action = "attempts.read" },
            Delegation(clock.UtcNow)
        ]));

        Assert.That(await policy.EvaluateAsync(Request()), Is.True);
        Assert.That(await policy.EvaluateAsync(Request(courseId: CourseL)), Is.False);
    }

    [TestCase("anonymous")]
    [TestCase("blocked-subject")]
    [TestCase("archived-subject")]
    [TestCase("unknown-subject-state")]
    [TestCase("invalid-subject-id")]
    [TestCase("empty-subject-id")]
    [TestCase("wrong-action")]
    [TestCase("action-case")]
    [TestCase("missing-resource")]
    [TestCase("wrong-resource-type")]
    [TestCase("resource-type-case")]
    [TestCase("invalid-course-id")]
    [TestCase("empty-course-id")]
    public async Task InvalidRequest_IsDeniedBeforeProviderLookup(string scenario)
    {
        memberships.Set(Membership());
        var request = Request();
        request = scenario switch
        {
            "anonymous" => request with { Subject = null },
            "blocked-subject" => request with { Subject = request.Subject! with { State = AccountState.Blocked } },
            "archived-subject" => request with { Subject = request.Subject! with { State = AccountState.Archived } },
            "unknown-subject-state" => request with { Subject = request.Subject! with { State = (AccountState)999 } },
            "invalid-subject-id" => request with { Subject = request.Subject! with { Id = "not-a-guid" } },
            "empty-subject-id" => request with { Subject = request.Subject! with { Id = Guid.Empty.ToString() } },
            "wrong-action" => request with { Action = "audit.events.write" },
            "action-case" => request with { Action = "Audit.Events.Read" },
            "missing-resource" => request with { Resource = null },
            "wrong-resource-type" => request with { Resource = new ResourceRef("Account", CourseK.ToString()) },
            "resource-type-case" => request with { Resource = new ResourceRef("course", CourseK.ToString()) },
            "invalid-course-id" => request with { Resource = new ResourceRef("Course", "not-a-guid") },
            "empty-course-id" => request with { Resource = new ResourceRef("Course", Guid.Empty.ToString()) },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        Assert.That(await policy.EvaluateAsync(request), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(identity.Calls, Is.Zero);
            Assert.That(memberships.Calls, Is.Zero);
        });
    }

    [TestCase(AccountStatus.Blocked)]
    [TestCase(AccountStatus.Archived)]
    [TestCase((AccountStatus)999)]
    public async Task AuthoritativeAccountStatus_OverridesActiveSubjectSnapshot(AccountStatus status)
    {
        identity.Set(TeacherId, status);
        memberships.Set(Membership());

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
        Assert.That(memberships.Calls, Is.Zero);
    }

    [Test]
    public async Task MissingAccount_IsDenied()
    {
        memberships.Set(Membership(accountId: OtherAccountId));

        Assert.That(await policy.EvaluateAsync(Request(accountId: OtherAccountId)), Is.False);
    }

    [Test]
    public async Task IdentityLookupReturningAnotherAccount_IsDenied()
    {
        identity.Handler = (_, _) => ValueTask.FromResult<AccountSnapshot?>(new(OtherAccountId, AccountStatus.Active));
        memberships.Set(Membership());

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [Test]
    public async Task AccountWithoutCourseMembership_HasNoImplicitAdministratorBypass()
    {
        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [TestCase("inactive")]
    [TestCase("other-account")]
    [TestCase("other-course")]
    public async Task InvalidMembership_CannotGrantTeacherAccess(string scenario)
    {
        var membership = Membership();
        membership = scenario switch
        {
            "inactive" => membership with { IsActive = false },
            "other-account" => membership with { AccountId = OtherAccountId },
            "other-course" => membership with { CourseId = CourseL },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        memberships.Handler = (_, _, _) => ValueTask.FromResult<CourseAuditMembership?>(membership);

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [Test]
    public async Task AccountBlockedBetweenRequests_IsDeniedOnNextEvaluation()
    {
        memberships.Set(Membership());
        var unchangedRequest = Request();
        Assert.That(await policy.EvaluateAsync(unchangedRequest), Is.True);

        identity.Set(TeacherId, AccountStatus.Blocked);

        Assert.That(await policy.EvaluateAsync(unchangedRequest), Is.False);
        Assert.That(identity.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task MembershipRevokedBetweenRequests_IsDeniedOnNextEvaluation()
    {
        memberships.Set(Membership());
        Assert.That(await policy.EvaluateAsync(Request()), Is.True);

        memberships.Set(Membership() with { IsActive = false });

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
        Assert.That(memberships.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task DelegationRevokedBetweenRequests_IsDeniedOnNextEvaluation()
    {
        var delegation = Delegation(clock.UtcNow);
        memberships.Set(Membership(CourseAuditRole.Assistant, delegations: [delegation]));
        Assert.That(await policy.EvaluateAsync(Request()), Is.True);

        memberships.Set(Membership(CourseAuditRole.Assistant,
            delegations: [delegation with { RevokedAt = clock.UtcNow }]));

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [Test]
    public async Task DelegationExpiresBetweenRequests_IsDeniedAtExpirationWithoutRestart()
    {
        var delegation = Delegation(clock.UtcNow);
        memberships.Set(Membership(CourseAuditRole.Assistant, delegations: [delegation]));
        Assert.That(await policy.EvaluateAsync(Request()), Is.True);

        clock.UtcNow = delegation.ExpiresAt;

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [TestCase("identity")]
    [TestCase("membership")]
    public async Task UnavailableProvider_DoesNotGrantAccess(string provider)
    {
        memberships.Set(Membership());
        if (provider == "identity")
            identity.Handler = (_, _) => ValueTask.FromException<AccountSnapshot?>(new InvalidOperationException("unavailable"));
        else
            memberships.Handler = (_, _, _) => ValueTask.FromException<CourseAuditMembership?>(new InvalidOperationException("unavailable"));

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [TestCase("identity")]
    [TestCase("membership")]
    public async Task ProviderCancellationWithoutCallerCancellation_DoesNotGrantAccess(string provider)
    {
        if (provider == "identity")
            identity.Handler = (_, _) => ValueTask.FromException<AccountSnapshot?>(new OperationCanceledException());
        else
            memberships.Handler = (_, _, _) => ValueTask.FromException<CourseAuditMembership?>(new OperationCanceledException());

        Assert.That(await policy.EvaluateAsync(Request()), Is.False);
    }

    [Test]
    public void AlreadyCancelledCallerToken_IsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await policy.EvaluateAsync(Request(), cancellation.Token));
    }

    [TestCase("identity")]
    [TestCase("membership")]
    public void CallerCancellationDuringLookup_IsPropagated(string provider)
    {
        using var cancellation = new CancellationTokenSource();
        if (provider == "identity")
            identity.Handler = (_, token) =>
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<AccountSnapshot?>(token);
            };
        else
            memberships.Handler = (_, _, token) =>
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<CourseAuditMembership?>(token);
            };

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await policy.EvaluateAsync(Request(), cancellation.Token));
    }

    [Test]
    public async Task ConcurrentSubjects_DoNotShareIdentityOrCourseState()
    {
        identity.Set(OtherAccountId, AccountStatus.Active);
        memberships.Set(Membership());
        memberships.Set(Membership(CourseAuditRole.Student, OtherAccountId, CourseL));
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        identity.Handler = async (accountId, token) =>
        {
            if (Interlocked.Increment(ref started) == 2)
                bothStarted.TrySetResult();
            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            return new AccountSnapshot(accountId, AccountStatus.Active);
        };

        var teacher = policy.EvaluateAsync(Request(correlationId: "teacher-request")).AsTask();
        var student = policy.EvaluateAsync(Request(OtherAccountId, CourseL, "student-request")).AsTask();
        await Task.WhenAll(teacher, student);

        Assert.Multiple(() =>
        {
            Assert.That(teacher.Result, Is.True);
            Assert.That(student.Result, Is.False);
            Assert.That(identity.Calls, Is.EqualTo(2));
            Assert.That(memberships.Calls, Is.EqualTo(2));
        });
    }
}
