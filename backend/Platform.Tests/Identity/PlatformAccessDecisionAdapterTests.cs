using Microsoft.AspNetCore.Http;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Api.Domain.Authentication;
using Platform.Api.Domain.Identity;
using Platform.Api.Services.Identity;
using PlatformAccessDecision = Platform.Access.Contracts.IAccessDecision;

namespace Platform.Tests.Identity;

[TestFixture]
public sealed class PlatformAccessDecisionAdapterTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private sealed class FakeEngine(Func<AccessRequest, ValueTask<AccessDecision>> decide) : PlatformAccessDecision
    {
        public List<AccessRequest> Requests { get; } = [];

        public ValueTask<AccessDecision> DecideAsync(AccessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return decide(request);
        }
    }

    private sealed class FakeAccounts(params UserAccount[] items) : IAccountRepository
    {
        public UserAccount? Find(Guid id) => items.FirstOrDefault(a => a.Id == id);

        public void Save(UserAccount account) => throw new NotSupportedException();
    }

    private static ValueTask<AccessDecision> Allow(AccessRequest r) =>
        ValueTask.FromResult(AccessDecision.Allow("p.id", r.CorrelationId, "v1"));

    private static ValueTask<AccessDecision> Deny(AccessRequest r) =>
        ValueTask.FromResult(AccessDecision.Deny(AccessDenialReason.PolicyError, r.CorrelationId, "v1"));

    private static CallerIdentity Caller(Guid? id = null, bool authenticated = true) =>
        new(authenticated, id ?? UserId, AuthenticationLevel.Active, null);

    private static PlatformAccessDecisionAdapter Adapter(
        FakeEngine engine,
        UserAccount[]? accounts = null,
        string? traceId = "trace-1") =>
        new(
            engine,
            new FakeAccounts(accounts ?? [new UserAccount(UserId, "u@example.com")]),
            new HttpContextAccessor { HttpContext = traceId is null ? null : new DefaultHttpContext { TraceIdentifier = traceId } });

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_EngineAllow_IsTrue()
    {
        Assert.That(Adapter(new FakeEngine(Allow)).CanChangeAccountStatus(Caller()), Is.True);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_EngineDeny_IsFalse()
    {
        Assert.That(Adapter(new FakeEngine(Deny)).CanChangeAccountStatus(Caller()), Is.False);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_AsksTheEngine_ForTheAccountStatusAction_WithCanonicalSubjectId()
    {
        var engine = new FakeEngine(Allow);

        Adapter(engine).CanChangeAccountStatus(Caller());

        var request = engine.Requests.Single();
        Assert.That(request.Action, Is.EqualTo("account.status.change"));
        Assert.That(request.Subject!.Id, Is.EqualTo("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        Assert.That(request.Subject.State, Is.EqualTo(AccountState.Active));
        Assert.That(request.CorrelationId, Is.EqualTo("trace-1"), "same id the use-case writes to its own audit");
    }

    [TestCase(AccountStatus.Blocked, AccountState.Blocked)]
    [TestCase(AccountStatus.Archived, AccountState.Archived)]
    [TestCase(AccountStatus.Active, AccountState.Active)]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_MapsAccountStatusFromTheRepository(AccountStatus status, AccountState expected)
    {
        var engine = new FakeEngine(Deny);

        Adapter(engine, [new UserAccount(UserId, "u@example.com", status)]).CanChangeAccountStatus(Caller());

        Assert.That(engine.Requests.Single().Subject!.State, Is.EqualTo(expected));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_UnknownAccount_IsNotActive()
    {
        var engine = new FakeEngine(Deny);

        Adapter(engine, accounts: []).CanChangeAccountStatus(Caller());

        Assert.That(engine.Requests.Single().Subject!.State, Is.Not.EqualTo(AccountState.Active));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_NotAuthenticatedCaller_HasNoSubject()
    {
        var engine = new FakeEngine(Deny);

        // MfaPending is not authenticated: its user id must never reach the engine as a subject.
        Adapter(engine).CanChangeAccountStatus(Caller(authenticated: false));

        Assert.That(engine.Requests.Single().Subject, Is.Null);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_AuthenticatedCallerWithoutUserId_HasNoSubject()
    {
        var engine = new FakeEngine(Deny);

        Adapter(engine).CanChangeAccountStatus(new CallerIdentity(true, null, AuthenticationLevel.Active, null));

        Assert.That(engine.Requests.Single().Subject, Is.Null);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_NoHttpContext_StillGetsACorrelationId()
    {
        var engine = new FakeEngine(Deny);

        Adapter(engine, traceId: null).CanChangeAccountStatus(Caller());

        Assert.That(engine.Requests.Single().CorrelationId, Is.Not.Empty);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_EngineThrowsSynchronously_IsFalse()
    {
        var engine = new FakeEngine(_ => throw new InvalidOperationException("boom"));

        Assert.That(Adapter(engine).CanChangeAccountStatus(Caller()), Is.False);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_EngineThrowsAsynchronously_IsFalse()
    {
        var engine = new FakeEngine(async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        });

        Assert.That(Adapter(engine).CanChangeAccountStatus(Caller()), Is.False);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_EngineCancelled_IsFalse()
    {
        var engine = new FakeEngine(_ => throw new OperationCanceledException());

        Assert.That(Adapter(engine).CanChangeAccountStatus(Caller()), Is.False);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_AsynchronousEngine_IsAwaitedToItsAnswer()
    {
        var engine = new FakeEngine(async r =>
        {
            await Task.Delay(50);
            return AccessDecision.Allow("p.id", r.CorrelationId, "v1");
        });

        Assert.That(Adapter(engine).CanChangeAccountStatus(Caller()), Is.True);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Adapter_RepositoryThrows_IsFalse()
    {
        var adapter = new PlatformAccessDecisionAdapter(
            new FakeEngine(Allow),
            new ThrowingAccounts(),
            new HttpContextAccessor());

        Assert.That(adapter.CanChangeAccountStatus(Caller()), Is.False);
    }

    private sealed class ThrowingAccounts : IAccountRepository
    {
        public UserAccount? Find(Guid id) => throw new InvalidOperationException("db down");

        public void Save(UserAccount account) => throw new NotSupportedException();
    }
}
