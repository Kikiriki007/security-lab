using Platform.Api.Domain.Authentication;

namespace Platform.Api.Services.Identity;

public sealed record SessionInfo(
    Guid Handle,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt);

public sealed record ValidateSession(
    Guid Handle,
    Guid UserId,
    AuthenticationLevel Level
);

public interface ISessionManager
{
    Task<int> RevokeAllForUser(Guid userId, string reason, CancellationToken ct = default);

    Task<bool> RevokeSession(Guid ownerUserId, Guid sessionHandle, CancellationToken ct = default);

    Task<ValidateSession?> ValidateAsync(string token, CancellationToken ct = default);

    Task<IReadOnlyList<SessionInfo>> ListForUser(Guid userId, CancellationToken ct = default);
}