using Platform.Api.Modules.Laboratories.Application;
using Platform.Api.Modules.Laboratories.Domain;
using Platform.Assignments.Contracts;
using Platform.Prerequisites.Contracts;

namespace Platform.Api.Modules.Laboratories.Infrastructure;

public sealed class LaboratoryPrerequisiteAccessAdapter
    : ILaboratoryPrerequisiteAccess
{
    private readonly ILaboratoryRepository _laboratoryRepository;
    private readonly IPrerequisiteAccessService _prerequisiteAccessService;

    public LaboratoryPrerequisiteAccessAdapter(
        ILaboratoryRepository laboratoryRepository,
        IPrerequisiteAccessService prerequisiteAccessService)
    {
        _laboratoryRepository = laboratoryRepository;
        _prerequisiteAccessService = prerequisiteAccessService;
    }

    public async ValueTask<bool> CanStartAsync(
        string studentId,
        string publishedLabRevisionId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(studentId, out Guid studentGuid) ||
            !Guid.TryParse(publishedLabRevisionId, out Guid revisionGuid))
        {
            return false;
        }

        LabRevision? revision =
            await _laboratoryRepository.GetRevisionByIdAsync(
                revisionGuid,
                cancellationToken);

        if (revision is null)
        {
            return false;
        }

        var result = await _prerequisiteAccessService.CheckAccessAsync(
            studentGuid,
            revision.LaboratoryId,
            cancellationToken);

        return result.IsUnlocked;
    }
}