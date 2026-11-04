using Platform.LabLocalization.Application;

namespace Platform.LabLocalization.Tests.Fakes;

/// <summary>Stands in for the revision module: a laboratory has the revision it was given here, or none.</summary>
public sealed class FakeRevisionReader : ILabRevisionReader
{
    private readonly Dictionary<Guid, RevisionInfo> _current = new();

    public void SetCurrent(Guid labId, RevisionInfo revision) => _current[labId] = revision;

    public Task<RevisionInfo?> FindCurrentForLabAsync(Guid labId, CancellationToken ct = default) =>
        Task.FromResult(_current.GetValueOrDefault(labId));
}
