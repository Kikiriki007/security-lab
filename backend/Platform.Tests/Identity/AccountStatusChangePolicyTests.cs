using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Platform.Access.Application.Services;
using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Access.Domain.ValueObjects;
using Platform.Access.Infrastructure.Auditing;
using Platform.Access.Infrastructure.Catalog;
using Platform.Access.Infrastructure.Policies;
using Platform.Api.Services.Identity;
using PlatformAccessDecision = Platform.Access.Contracts.IAccessDecision;

namespace Platform.Tests.Identity;

[TestFixture]
public sealed class AccountStatusChangePolicyTests
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();

    private sealed class FakeAdmins(params Guid[] ids) : IAdminDirectory
    {
        public bool IsAdministrator(Guid userId) => ids.Contains(userId);
    }

    private sealed class EveryoneIsAdmin : IAdminDirectory
    {
        public bool IsAdministrator(Guid userId) => true;
    }

    private sealed class BrokenDirectory : IAdminDirectory
    {
        public bool IsAdministrator(Guid userId) => throw new InvalidOperationException("directory down");
    }

    private static AccessRequest Request(string? subjectId, AccountState state = AccountState.Active) =>
        new(subjectId is null ? null : new SubjectRef(subjectId, state), AccountStatusChangePolicy.ActionId, null, "corr-1");

    private static ValueTask<bool> Evaluate(IAdminDirectory admins, string? subjectId) =>
        new AccountStatusChangePolicy(admins).EvaluateAsync(Request(subjectId));

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Policy_AdminPermitted()
    {
        Assert.That(await Evaluate(new FakeAdmins(AdminId), AdminId.ToString()), Is.True);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Policy_UppercaseGuidOfAdminPermitted()
    {
        Assert.That(await Evaluate(new FakeAdmins(AdminId), AdminId.ToString().ToUpperInvariant()), Is.True);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Policy_NonAdminDenied()
    {
        Assert.That(await Evaluate(new FakeAdmins(AdminId), StudentId.ToString()), Is.False);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Policy_NoSubjectDenied()
    {
        Assert.That(await Evaluate(new EveryoneIsAdmin(), null), Is.False);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-a-guid")]
    [TestCase("admin")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Policy_UnparsableOrEmptySubjectDenied_EvenIfDirectoryAcceptsEveryone(string subjectId)
    {
        Assert.That(await Evaluate(new EveryoneIsAdmin(), subjectId), Is.False);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Policy_OnlyCanonicalGuidFormat_IsAccepted()
    {
        var admins = new FakeAdmins(AdminId);

        Assert.That(await Evaluate(admins, AdminId.ToString("N")), Is.False, "N format");
        Assert.That(await Evaluate(admins, AdminId.ToString("B")), Is.False, "braces");
        Assert.That(await Evaluate(admins, " " + AdminId + " "), Is.False, "padded");
        Assert.That(await Evaluate(admins, AdminId + "\n"), Is.False, "trailing newline");
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Policy_IdAndVersion_MatchAuditCatalogFormats()
    {
        var policy = new AccountStatusChangePolicy(new FakeAdmins());

        Assert.That(policy.Id, Does.Match(new Regex(@"^[a-z][a-z0-9_.-]{0,63}\z")));
        Assert.That(policy.Version, Does.Match(new Regex(@"^v[0-9]{1,4}(\.[0-9]{1,4}){0,2}\z")));
        Assert.That(policy.Id, Is.EqualTo("identity.account.status.change.admin-only"));
        Assert.That(policy.Version, Is.EqualTo("v1"));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_ActionId_IsValid()
    {
        Assert.That(ActionId.IsValid(AccountStatusChangePolicy.ActionId), Is.True);
        Assert.That(AccountStatusChangePolicy.ActionId, Is.EqualTo("account.status.change"));
    }

    // Real catalog, registry and engine; only the admin directory is replaced.
    private static (IServiceProvider Services, PlatformAccessDecision Decision) Compose(IAdminDirectory admins)
    {
        var services = new ServiceCollection();
        services.AddSingleton(admins);
        services.AddSingleton<InMemoryActionCatalog>();
        services.AddSingleton<InMemoryAccessPolicyRegistry>();
        var provider = services.BuildServiceProvider();

        provider.RegisterAccountStatusAccess();

        var decision = new DefaultDenyAccessDecision(
            provider.GetRequiredService<InMemoryActionCatalog>(),
            provider.GetRequiredService<InMemoryAccessPolicyRegistry>(),
            new NullAccessAuditWriter());
        return (provider, decision);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Registration_AddsActionAndPolicy()
    {
        var (provider, _) = Compose(new FakeAdmins(AdminId));

        Assert.That(provider.GetRequiredService<InMemoryActionCatalog>().Contains(AccountStatusChangePolicy.ActionId), Is.True);
        Assert.That(
            provider.GetRequiredService<InMemoryAccessPolicyRegistry>().TryGet(AccountStatusChangePolicy.ActionId, out var policy),
            Is.True);
        Assert.That(policy, Is.TypeOf<AccountStatusChangePolicy>());
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Engine_AdminAllowed_WithPolicyIdAndVersion()
    {
        var (_, decision) = Compose(new FakeAdmins(AdminId));

        var result = await decision.DecideAsync(Request(AdminId.ToString()));

        Assert.That(result.Outcome, Is.EqualTo(AccessOutcome.Allow));
        Assert.That(result.PolicyId, Is.EqualTo("identity.account.status.change.admin-only"));
        Assert.That(result.PolicyVersion, Is.EqualTo("v1"));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Engine_NonAdminDenied()
    {
        var (_, decision) = Compose(new FakeAdmins(AdminId));

        var result = await decision.DecideAsync(Request(StudentId.ToString()));

        Assert.That(result.Outcome, Is.EqualTo(AccessOutcome.Deny));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Engine_BlockedAdminDenied_BeforeThePolicyRuns()
    {
        var (_, decision) = Compose(new FakeAdmins(AdminId));

        var result = await decision.DecideAsync(Request(AdminId.ToString(), AccountState.Blocked));

        Assert.That(result.Outcome, Is.EqualTo(AccessOutcome.Deny));
        Assert.That(result.DenialReason, Is.EqualTo(AccessDenialReason.AccountNotActive));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Engine_AnonymousDenied_AsUnauthenticated()
    {
        var (_, decision) = Compose(new EveryoneIsAdmin());

        var result = await decision.DecideAsync(Request(null));

        Assert.That(result.DenialReason, Is.EqualTo(AccessDenialReason.Unauthenticated));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Engine_BrokenDirectory_Denies()
    {
        var (_, decision) = Compose(new BrokenDirectory());

        var result = await decision.DecideAsync(Request(AdminId.ToString()));

        Assert.That(result.Outcome, Is.EqualTo(AccessOutcome.Deny));
    }
}
