using Team21.Acc.Core.Courses;

namespace Team21.Acc.Infrastructure.Courses.Authorization;

public sealed class DevelopmentCourseAuthorization : ICourseAuthorization
{
    // Development-only test identity.
    private static readonly Guid TestManagerId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    public Task<bool> CanCreateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(userId == TestManagerId);
    }

    public Task<bool> CanArchiveAsync(
        Guid userId,
        Guid courseId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(userId == TestManagerId);
    }
}