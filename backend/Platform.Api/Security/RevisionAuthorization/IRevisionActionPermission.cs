namespace Platform.Api.Security.RevisionAuthorization;

/// <summary>
/// Checks explicit permissions for revision actions.
/// The implementation must use trusted server-side authorization data.
/// </summary>
public interface IRevisionActionPermission
{
    ValueTask<bool> IsAllowedAsync(
        Guid userId,
        string action,
        Guid resourceId,
        CancellationToken cancellationToken = default);
}
