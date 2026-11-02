namespace Team21.Acc.Core.Groups.Visibility;

/// <summary>Why a caller was denied a material.</summary>
public enum DenialReason
{
    UnknownAction = 1,
    AccountNotActive = 2,
    NoRule = 3,
    NotCourseMember = 4,
    NotInGroup = 5,
    UnknownVisibility = 6
}

/// <summary>Audit record for one denial. Holds no material content.</summary>
public sealed record VisibilityDenial(
    Guid? ActorId,
    MaterialRef Material,
    DenialReason Reason,
    string CorrelationId);
