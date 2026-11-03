
using Platform.Authoring.Domain;

namespace Platform.Authoring.Infrastructure.Persistence.Entities;

public sealed class LaboratoryRecord
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    public LabType Type { get; set; }

    public List<string> Tags { get; set; } = new();

    public List<LabPrerequisiteRecord> Prerequisites { get; set; } = new();
}
