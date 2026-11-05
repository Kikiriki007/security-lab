using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;

namespace Platform.Api.Security.RevisionAuthorization;

public sealed class RevisionApprovalPolicy(
    IRevisionAuthorReader revisionAuthorReader,
    IRevisionApprovalPermission approvalPermission) : IAccessPolicy
{
    public const string Action = "revision.approve";

    public string Id => "rev05.revision-approval";
    public string Version => "rev05:v1";

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

        if (request.Action != Action ||
            request.Resource?.Type != "revision" ||
            !Guid.TryParse(request.Resource.Id, out var revisionId) ||
            revisionId == Guid.Empty)
        {
            return false;
        }

        var authorId = await revisionAuthorReader.FindAuthorIdAsync(
            revisionId,
            cancellationToken);

        if (authorId is null || authorId == Guid.Empty || authorId == userId)
        {
            return false;
        }

        return await approvalPermission.CanApproveAsync(
            userId,
            revisionId,
            cancellationToken);
    }
}
