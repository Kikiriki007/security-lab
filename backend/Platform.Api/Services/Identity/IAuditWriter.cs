namespace Platform.Api.Services.Identity;

public sealed record AuditEvent(
    DateTimeOffset Time,
    Guid? ActorId,
    string Action,
    string Target,
    string Outcome,
    string CorrelationId,
    string? Detail);

public interface IAuditWriter
{
    void Write(AuditEvent e);
}