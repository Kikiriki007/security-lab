using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Access.Contracts;
using Platform.Access.Domain.Models;

namespace Platform.Access.Infrastructure.Auditing;

public sealed class LoggerAccessAuditWriter(
    ILogger<LoggerAccessAuditWriter>? logger = null) : IAccessAuditWriter
{
    private readonly ILogger<LoggerAccessAuditWriter> logger =
        logger ?? NullLogger<LoggerAccessAuditWriter>.Instance;

    public ValueTask WriteAsync(
        AccessRequest request,
        AccessDecision decision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Authorization decision. Outcome: {Outcome}; Reason: {Reason}; Action: {Action}; " +
            "ResourceType: {ResourceType}; ResourceIdHash: {ResourceIdHash}; SubjectIdHash: {SubjectIdHash}; " +
            "PolicyId: {PolicyId}; PolicyVersion: {PolicyVersion}; CorrelationId: {CorrelationId}",
            decision.Outcome,
            decision.DenialReason,
            Sanitize(request.Action),
            Sanitize(request.Resource?.Type),
            Hash(request.Resource?.Id),
            Hash(request.Subject?.Id),
            Sanitize(decision.PolicyId),
            Sanitize(decision.PolicyVersion),
            Sanitize(decision.CorrelationId));

        return ValueTask.CompletedTask;
    }

    private static string Sanitize(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : new string(value.Select(character =>
                char.IsControl(character) ? '_' : character).Take(128).ToArray());

    private static string Hash(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(digest)[..16];
    }
}
