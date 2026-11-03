
using Platform.Authoring.Application.Models;

namespace Platform.Authoring.Application;

public interface ILaboratoryRepository
{
    Task<LaboratoryData?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default);

    Task AddAsync(
        LaboratoryData laboratory,
        CancellationToken ct = default);

    Task UpdateAsync(
        LaboratoryData laboratory,
        CancellationToken ct = default);

    Task AddPrerequisiteAsync(
        Guid laboratoryId,
        Guid prerequisiteLabId,
        CancellationToken ct = default);

    Task RemovePrerequisiteAsync(
        Guid laboratoryId,
        Guid prerequisiteLabId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetPrerequisiteIdsAsync(
        Guid laboratoryId,
        CancellationToken ct = default);
}
