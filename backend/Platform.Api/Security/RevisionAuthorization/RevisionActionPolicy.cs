using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;

namespace Platform.Api.Security.RevisionAuthorization;

public sealed class RevisionActionPolicy(
    IRevisionActionPermission permission) : IAccessPolicy
{
    public string Id => "rev05.explicit-actions";
    public string Version => "v1";

    public async ValueTask<bool> EvaluateAsync(
        AccessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Subject is null ||
            request.Subject.State != AccountState.Active ||
            !Guid.TryParse(request.Subject.Id, out var userId) ||
            userId == Guid.Empty)
        {
            return false;
        }

        string expectedResourceType = request.Action switch
        {
            RevisionActions.Submit => "revision",
            RevisionActions.Return => "revision",
            RevisionActions.UnlockAccount => "account",
            _ => ""
        };

        if (expectedResourceType.Length == 0 ||
            request.Resource?.Type != expectedResourceType ||
            !Guid.TryParse(request.Resource.Id, out var resourceId) ||
            resourceId == Guid.Empty)
        {
            return false;
        }

        return await permission.IsAllowedAsync(
            userId,
            request.Action,
            resourceId,
            cancellationToken);
    }
}

