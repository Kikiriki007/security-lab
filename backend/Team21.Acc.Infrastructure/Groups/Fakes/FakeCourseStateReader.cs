using Team21.Acc.Core.Groups.Visibility;
using Team21.Acc.Core.Groups.Visibility.Interfaces;

namespace Team21.Acc.Infrastructure.Groups.Fakes;

/// <summary>Development only. Stands in for the ACC-01 course status.</summary>
public sealed class FakeCourseStateReader : ICourseStateReader
{
    private readonly FakeDirectory _directory;

    public FakeCourseStateReader(FakeDirectory directory)
    {
        _directory = directory;
    }

    public Task<CourseState?> GetStateAsync(Guid courseId, CancellationToken ct = default) =>
        Task.FromResult(_directory.StateOf(courseId));
}
