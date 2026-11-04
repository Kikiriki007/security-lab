using Team21.Acc.Core.Groups.Visibility;
using Team21.Acc.Core.Groups.Visibility.Interfaces;

namespace Team21.Acc.Infrastructure.Groups.Defaults;

/// <summary>
/// Used outside Development until ACC-01 is connected. No course is known.
/// </summary>
public sealed class NoCourseStateReader : ICourseStateReader
{
    public Task<CourseState?> GetStateAsync(Guid courseId, CancellationToken ct = default) =>
        Task.FromResult<CourseState?>(null);
}
