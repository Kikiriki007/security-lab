namespace Team21.Acc.Core.Courses;

public interface ICourseAuthorization
{
    Task<bool> CanCreateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> CanArchiveAsync(
        Guid userId,
        Guid courseId,
        CancellationToken cancellationToken = default);
}