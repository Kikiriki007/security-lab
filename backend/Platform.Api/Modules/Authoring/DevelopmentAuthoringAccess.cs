using Platform.Authoring.Application;
using Platform.Api.Modules.Laboratories.Application;

namespace Platform.Api.Modules.Authoring;

//Implementing the access contract using the identity context
//registered by LaboratoriesModule

public sealed class DevelopmentAuthoringAcces(IIdentityContext identity) : IAuthoringAccess
{
    public bool IsAllowed(Guid actorId, AuthoringAction action, Guid? resourceId)
    {
        //DraftService supplies the actor ID
        //said ID must match the current request's identity, whose ID must be a valid, non-empty GUID
        if(!identity.IsAuthenticated || !Guid.TryParse(identity.UserId, out Guid currentUserId) || currentUserId == Guid.Empty || currentUserId != actorId)
        {
            return false;
        }

        //Allow only template-based creation by a teacher or a TA
        //DraftService checks whether the template exists after authorization
        return action == AuthoringAction.CreateDraftFromTemplate && resourceId.HasValue
        && resourceId.Value != Guid.Empty && identity.Role is PlatformRole.Teacher or PlatformRole.Assistant;
    }
}