
using Platform.Authoring.Domain;

namespace Platform.Authoring.Application.Models;

public sealed record LaboratoryData(
    Guid Id,
    string Title,
    string Goal,
    string Difficulty,
    LabType Type,
    IReadOnlyList<string> Tags
);
