using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Platform.Access.Application.Services;
using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Infrastructure.Catalog;
using Platform.Access.Infrastructure.Policies;
using Platform.Audit.Application.Authorization;
using Platform.Audit.Contracts;
using Platform.Identity.Contracts;
using Platform.Identity.Domain.Enums;
using static Platform.Audit.Tests.Authorization.AuditAuthorizationExamples;

namespace Platform.Audit.Tests.Authorization;

// These registrations exist only in a test harness. They do not alter Access or the host.
[TestFixture]
public sealed class CourseAuditAccessDecisionTests
{
    [Test]
    public async Task ScopedPolicy_UsesIndependentScopedProvidersUnderStrictValidation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new AuditAuthorizationClock());
        services.AddScoped<IIdentityContext>(_ =>
        {
            var stub = new StubAuditIdentity();
            stub.Set(TeacherId, AccountStatus.Active);
            return stub;
        });
        services.AddScoped<ICourseAuditMembershipReader>(_ =>
        {
            var stub = new StubCourseAuditMemberships();
            stub.Set(Membership());
            return stub;
        });
        services.AddScoped<IAccessPolicy, CourseAuditReadPolicy>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var firstPolicy = firstScope.ServiceProvider.GetRequiredService<IAccessPolicy>();
        var secondPolicy = secondScope.ServiceProvider.GetRequiredService<IAccessPolicy>();

        Assert.That(await firstPolicy.EvaluateAsync(Request()), Is.True);
        Assert.That(await secondPolicy.EvaluateAsync(Request()), Is.True);
        ((StubCourseAuditMemberships)firstScope.ServiceProvider.GetRequiredService<ICourseAuditMembershipReader>())
            .Set(Membership() with { IsActive = false });

        Assert.That(await firstPolicy.EvaluateAsync(Request()), Is.False);
        Assert.That(await secondPolicy.EvaluateAsync(Request()), Is.True);
        Assert.That(firstPolicy, Is.Not.SameAs(secondPolicy));
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IAccessPolicy>());
    }

    [Test]
    public async Task RegisteredPolicy_UsesRealAccessDecision_AndAuditsEachDecisionOnce()
    {
        var identity = new StubAuditIdentity();
        identity.Set(TeacherId, AccountStatus.Active);
        var memberships = new StubCourseAuditMemberships();
        memberships.Set(Membership());
        memberships.Set(Membership(CourseAuditRole.Student, courseId: CourseL));
        var policy = new CourseAuditReadPolicy(identity, memberships, new AuditAuthorizationClock());
        var catalog = new InMemoryActionCatalog();
        catalog.Register(CourseAuditReadPolicy.ActionId);
        var policies = new InMemoryAccessPolicyRegistry();
        policies.Register(CourseAuditReadPolicy.ActionId, policy);
        var audit = new RecordingAccessAuditWriter();
        var access = new DefaultDenyAccessDecision(catalog, policies, audit);

        var allowed = await access.DecideAsync(Request(correlationId: "allowed-request"));
        var denied = await access.DecideAsync(Request(courseId: CourseL, correlationId: "denied-request"));

        Assert.Multiple(() =>
        {
            Assert.That(allowed.Outcome, Is.EqualTo(AccessOutcome.Allow));
            Assert.That(allowed.PolicyId, Is.EqualTo("audit.course.read"));
            Assert.That(allowed.PolicyVersion, Is.EqualTo("v1"));
            Assert.That(allowed.CorrelationId, Is.EqualTo("allowed-request"));
            Assert.That(denied.Outcome, Is.EqualTo(AccessOutcome.Deny));
            Assert.That(denied.CorrelationId, Is.EqualTo("denied-request"));
            Assert.That(audit.Entries, Has.Count.EqualTo(2));
            Assert.That(audit.Entries.Select(entry => entry.Decision), Is.EqualTo(new[] { allowed, denied }));
        });
    }

    [TestCase(false, AccessDenialReason.UnknownAction)]
    [TestCase(true, AccessDenialReason.NoPolicy)]
    public async Task MissingRegistration_KeepsAccessClosed(bool registerAction, AccessDenialReason expectedReason)
    {
        var catalog = new InMemoryActionCatalog();
        if (registerAction)
            catalog.Register(CourseAuditReadPolicy.ActionId);
        var audit = new RecordingAccessAuditWriter();
        var access = new DefaultDenyAccessDecision(catalog, new InMemoryAccessPolicyRegistry(), audit);

        var result = await access.DecideAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AccessOutcome.Deny));
            Assert.That(result.DenialReason, Is.EqualTo(expectedReason));
            Assert.That(audit.Entries, Has.Count.EqualTo(1));
        });
    }
}
