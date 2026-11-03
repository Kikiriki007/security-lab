
using Microsoft.EntityFrameworkCore;
using Platform.Authoring.Application;
using Platform.Authoring.Application.Models;
using Platform.Authoring.Infrastructure.Persistence.Entities;

namespace Platform.Authoring.Infrastructure.Persistence;

public sealed class LaboratoryRepository(
    AuthoringDbContext context) : ILaboratoryRepository
{
    // LAB-02: Read laboratory metadata
    public async Task<LaboratoryData?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var laboratory = await context.Laboratories
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (laboratory is null)
            return null;

        return ToData(laboratory);
    }

    // LAB-02: Persist a new laboratory
    public async Task AddAsync(
        LaboratoryData laboratory,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(laboratory);

        var record = new LaboratoryRecord
        {
            Id = laboratory.Id,
            Title = laboratory.Title,
            Goal = laboratory.Goal,
            Difficulty = laboratory.Difficulty,
            Type = laboratory.Type,
            Tags = laboratory.Tags.ToList()
        };

        await context.Laboratories.AddAsync(record, ct);
        await context.SaveChangesAsync(ct);
    }

    // LAB-02: Update metadata while preserving the ID
    public async Task UpdateAsync(
        LaboratoryData laboratory,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(laboratory);

        var existing = await context.Laboratories
            .FirstOrDefaultAsync(l => l.Id == laboratory.Id, ct);

        if (existing is null)
        {
            throw new KeyNotFoundException(
                $"Laboratory {laboratory.Id} was not found.");
        }

        existing.Title = laboratory.Title;
        existing.Goal = laboratory.Goal;
        existing.Difficulty = laboratory.Difficulty;
        existing.Type = laboratory.Type;
        existing.Tags = laboratory.Tags.ToList();

        await context.SaveChangesAsync(ct);
    }

    // LAB-06: Add prerequisite
    public async Task AddPrerequisiteAsync(
        Guid laboratoryId,
        Guid prerequisiteLabId,
        CancellationToken ct = default)
    {
        if (laboratoryId == prerequisiteLabId)
        {
            throw new ArgumentException(
                "A laboratory cannot require itself.");
        }

        var relationship = new LabPrerequisiteRecord
        {
            LaboratoryId = laboratoryId,
            PrerequisiteLabId = prerequisiteLabId
        };

        await context.LabPrerequisites.AddAsync(relationship, ct);
        await context.SaveChangesAsync(ct);
    }

    // LAB-06: Remove prerequisite
    public async Task RemovePrerequisiteAsync(
        Guid laboratoryId,
        Guid prerequisiteLabId,
        CancellationToken ct = default)
    {
        var relationship = await context.LabPrerequisites
            .FirstOrDefaultAsync(p =>
                p.LaboratoryId == laboratoryId &&
                p.PrerequisiteLabId == prerequisiteLabId,
                ct);

        if (relationship is null)
            return;

        context.LabPrerequisites.Remove(relationship);
        await context.SaveChangesAsync(ct);
    }

    // LAB-06: Read prerequisite IDs
    public async Task<IReadOnlyList<Guid>> GetPrerequisiteIdsAsync(
        Guid laboratoryId,
        CancellationToken ct = default)
    {
        return await context.LabPrerequisites
            .AsNoTracking()
            .Where(p => p.LaboratoryId == laboratoryId)
            .Select(p => p.PrerequisiteLabId)
            .ToListAsync(ct);
    }

    // Internal mapping: persistence entity -> application model
    private static LaboratoryData ToData(LaboratoryRecord record)
    {
        return new LaboratoryData(
            record.Id,
            record.Title,
            record.Goal,
            record.Difficulty,
            record.Type,
            record.Tags.ToList()
        );
    }
}
