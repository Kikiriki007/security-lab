using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Access.Infrastructure.Auditing;
using Platform.Access.Tests.Support;

namespace Platform.Access.Tests.Auditing;

[TestFixture]
public sealed class AccessAuditRecordFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] AllowedDenialReasons =
        ["unauthenticated", "account_not_active", "unknown_action", "no_policy", "policy_error"];

    private static AccessAuditRecordFactory CreateFactory() =>
        new(new ManualTimeProvider(Now), new AccessAuditOptions());

    private static AccessRequest Request(
        string action = "lab.read",
        SubjectRef? subject = null,
        ResourceRef? resource = null,
        string correlationId = "corr-1") =>
        new(subject ?? new SubjectRef("student-1", AccountState.Active), action, resource, correlationId);

    private static AccessDecision Denied(AccessDenialReason reason = AccessDenialReason.NoPolicy) =>
        AccessDecision.Deny(reason, "corr-1");

    private static IEnumerable<string?> AllValues(AccessAuditRecord record) =>
    [
        record.EventType, record.Actor, record.Action, record.Target, record.Outcome,
        record.CorrelationId, record.DenialReason, record.PolicyId, record.PolicyVersion,
    ];

    [Test]
    [Property("RequirementId", "ACC-05")]
    [Property("RequirementId", "AUD-03")]
    public void ACC05_ActionWithCrLf_NoForgedLine()
    {
        var record = CreateFactory().Create(
            Request(action: "lab.read\r\n2026-11-03 access.allowed actor=admin"),
            Denied(AccessDenialReason.UnknownAction));

        Assert.That(record.Action, Is.EqualTo(AccessAuditRecord.Invalid));
        Assert.That(AllValues(record), Has.None.Matches<string?>(v => v is not null && v.Any(char.IsControl)));
    }

    [TestCase("lab.read\n")]
    [TestCase("Lab.Read")]
    [TestCase("lab.read ")]
    [TestCase("lab")]
    [TestCase("lab.read\0")]
    [TestCase("")]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_InvalidAction_IsLoggedAsInvalid_NotRaw(string action)
    {
        var record = CreateFactory().Create(Request(action: action), Denied(AccessDenialReason.UnknownAction));

        Assert.That(record.Action, Is.EqualTo(AccessAuditRecord.Invalid));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_ValidAction_IsLoggedAsIs()
    {
        var record = CreateFactory().Create(Request(action: "attempt.read.course"), Denied());

        Assert.That(record.Action, Is.EqualTo("attempt.read.course"));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_ActorWithControlCharacters_IsSanitized()
    {
        var record = CreateFactory().Create(
            Request(subject: new SubjectRef("user\r\n-1", AccountState.Active)),
            Denied());

        Assert.That(record.Actor, Is.EqualTo("user-1"));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_LongActor_IsTruncated()
    {
        var record = CreateFactory().Create(
            Request(subject: new SubjectRef(new string('x', 5_000), AccountState.Active)),
            Denied());

        Assert.That(record.Actor, Has.Length.EqualTo(64));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_AnonymousSubject_IsLoggedAsAnonymous()
    {
        var request = new AccessRequest(null, "lab.read", null, "corr-1");

        var record = CreateFactory().Create(request, Denied(AccessDenialReason.Unauthenticated));

        Assert.That(record.Actor, Is.EqualTo(AccessAuditRecord.Anonymous));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_SubjectWithEmptyId_IsLoggedAsInvalid()
    {
        var record = CreateFactory().Create(
            Request(subject: new SubjectRef("\r\n", AccountState.Active)),
            Denied());

        Assert.That(record.Actor, Is.EqualTo(AccessAuditRecord.Invalid));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Resource_IsLoggedAsTypeColonId()
    {
        var record = CreateFactory().Create(Request(resource: new ResourceRef("attempt", "42")), Denied());

        Assert.That(record.Target, Is.EqualTo("attempt:42"));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_NoResource_IsLoggedAsDash()
    {
        var record = CreateFactory().Create(Request(), Denied());

        Assert.That(record.Target, Is.EqualTo(AccessAuditRecord.NoValue));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_ResourceWithControlCharacters_IsSanitizedAndBounded()
    {
        var record = CreateFactory().Create(
            Request(resource: new ResourceRef("attempt\n", new string('9', 500) + "\r\n")),
            Denied());

        Assert.That(record.Target, Does.StartWith("attempt:"));
        Assert.That(record.Target, Has.Length.LessThanOrEqualTo(64));
        Assert.That(record.Target.Any(char.IsControl), Is.False);
    }

    [TestCase(AccessDenialReason.Unauthenticated, "unauthenticated")]
    [TestCase(AccessDenialReason.AccountNotActive, "account_not_active")]
    [TestCase(AccessDenialReason.UnknownAction, "unknown_action")]
    [TestCase(AccessDenialReason.NoPolicy, "no_policy")]
    [TestCase(AccessDenialReason.PolicyError, "policy_error")]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_DenialReason_MapsToSnakeCase(AccessDenialReason reason, string expected)
    {
        var record = CreateFactory().Create(Request(), Denied(reason));

        Assert.That(record.DenialReason, Is.EqualTo(expected));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_EveryDenialReason_MapsToAuditCatalogValue()
    {
        // Guards against a new enum value that silently produces a value the Audit module rejects.
        var factory = CreateFactory();

        foreach (var reason in Enum.GetValues<AccessDenialReason>().Where(r => r != AccessDenialReason.None))
        {
            var record = factory.Create(Request(), Denied(reason));
            Assert.That(AllowedDenialReasons, Does.Contain(record.DenialReason), reason.ToString());
        }
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_UndefinedDenialReason_MapsToPolicyError()
    {
        var record = CreateFactory().Create(Request(), Denied((AccessDenialReason)999));

        Assert.That(record.DenialReason, Is.EqualTo("policy_error"));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Deny_HasDeniedEventTypeAndOutcome()
    {
        var record = CreateFactory().Create(Request(), Denied());

        Assert.That(record.EventType, Is.EqualTo(AccessAuditRecord.DeniedEventType));
        Assert.That(record.Outcome, Is.EqualTo("deny"));
        Assert.That(record.PolicyId, Is.Null);
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Allow_HasAllowedEventTypePolicyAndNoReason()
    {
        var record = CreateFactory().Create(
            Request(action: "account.status.change"),
            AccessDecision.Allow("identity.account.status.change.admin-only", "corr-1", "v1"));

        Assert.That(record.EventType, Is.EqualTo(AccessAuditRecord.PrivilegedAllowedEventType));
        Assert.That(record.Outcome, Is.EqualTo("allow"));
        Assert.That(record.DenialReason, Is.Null);
        Assert.That(record.PolicyId, Is.EqualTo("identity.account.status.change.admin-only"));
        Assert.That(record.PolicyVersion, Is.EqualTo("v1"));
    }

    [TestCase("Policy With Spaces")]
    [TestCase("policy\r\nid")]
    [TestCase("UPPER")]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_InvalidPolicyId_IsLoggedAsInvalid(string policyId)
    {
        var record = CreateFactory().Create(Request(), AccessDecision.Allow(policyId, "corr-1", "v1"));

        Assert.That(record.PolicyId, Is.EqualTo(AccessAuditRecord.Invalid));
    }

    [TestCase("1.0")]
    [TestCase("v1\n")]
    [TestCase("latest")]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_InvalidPolicyVersion_IsLoggedAsInvalid(string version)
    {
        var record = CreateFactory().Create(Request(), AccessDecision.Deny(AccessDenialReason.PolicyError, "corr-1", version));

        Assert.That(record.PolicyVersion, Is.EqualTo(AccessAuditRecord.Invalid));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    [Property("RequirementId", "OPS-08")]
    public void ACC05_CorrelationId_IsCarriedSanitized()
    {
        var record = CreateFactory().Create(Request(correlationId: "abc\r\n123"), Denied());

        Assert.That(record.CorrelationId, Is.EqualTo("abc123"));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_MissingCorrelationId_IsLoggedAsDash()
    {
        var record = CreateFactory().Create(Request(correlationId: ""), Denied());

        Assert.That(record.CorrelationId, Is.EqualTo(AccessAuditRecord.NoValue));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Time_ComesFromServerClock()
    {
        var record = CreateFactory().Create(Request(), Denied());

        Assert.That(record.Time, Is.EqualTo(Now));
        Assert.That(record.Count, Is.EqualTo(1));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    [Property("RequirementId", "AUD-03")]
    public void ACC05_Record_HasOnlyWhitelistedFields()
    {
        // Adding a field (for example a request body) must be a deliberate, reviewed change.
        string[] whitelist =
        [
            "Time", "EventType", "Actor", "Action", "Target", "Outcome",
            "CorrelationId", "DenialReason", "PolicyId", "PolicyVersion", "Count",
        ];

        var properties = typeof(AccessAuditRecord)
            .GetProperties()
            .Where(p => p.DeclaringType == typeof(AccessAuditRecord) && p.Name != "EqualityContract")
            .Select(p => p.Name);

        Assert.That(properties, Is.EquivalentTo(whitelist));
    }
}
