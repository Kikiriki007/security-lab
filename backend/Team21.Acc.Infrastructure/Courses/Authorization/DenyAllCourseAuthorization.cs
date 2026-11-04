using Team21.Acc.Core.Courses;

namespace Team21.Acc.Infrastructure.Courses.Authorization;

public sealed class DenyAllCourseAuthorization : ICourseAuthorization
{
    public Task<bool> CanCreateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> CanArchiveAsync(
        Guid userId,
        Guid courseId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}