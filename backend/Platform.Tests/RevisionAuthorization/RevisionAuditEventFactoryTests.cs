using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Api.Security.RevisionAuthorization;
using Platform.Audit.Contracts;

namespace Platform.Tests.RevisionAuthorization;

[TestFixture]
public sealed class RevisionAuditEventFactoryTests
{
    private static AccessRequest CreateRequest(string action)
    {
        return new AccessRequest(
            new SubjectRef(Guid.NewGuid().ToString(), AccountState.Active),
            action,
            new ResourceRef("revision", Guid.NewGuid().ToString()),
            "test-correlation");
    }

    [TestCase(RevisionActions.Submit)]
    [TestCase(RevisionActions.Approve)]
    [TestCase(RevisionActions.Return)]
    [TestCase(RevisionActions.UnlockAccount)]
    public void DeniedRevisionAction_CreatesAuditEvent(string action)
    {
        var request = CreateRequest(action);
        var decision = AccessDecision.Deny(
            AccessDenialReason.PolicyError,
            request.CorrelationId,
            "v1");

        var result = RevisionAuditEventFactory.CreateDeniedEvent(request, decision);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.EventType, Is.EqualTo(AuditEventTypes.AccessDenied));
        Assert.That(result.Outcome, Is.EqualTo(AuditOutcome.Denied));
        Assert.That(result.Details!["action"], Is.EqualTo(action));
        Assert.That(result.Details["denialReason"], Is.EqualTo("policy_error"));
    }

    [Test]
    public void AllowedAction_DoesNotCreateDeniedEvent()
    {
        var request = CreateRequest(RevisionActions.Approve);
        var decision = AccessDecision.Allow("rev05.revision-approval", request.CorrelationId, "v1");

        var result = RevisionAuditEventFactory.CreateDeniedEvent(request, decision);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void UnknownAction_DoesNotCreateRevisionAuditEvent()
    {
        var request = CreateRequest("revision.delete");
        var decision = AccessDecision.Deny(
            AccessDenialReason.UnknownAction, request.CorrelationId);

        var result = RevisionAuditEventFactory.CreateDeniedEvent(request, decision);

        Assert.That(result, Is.Null);
    }
}
