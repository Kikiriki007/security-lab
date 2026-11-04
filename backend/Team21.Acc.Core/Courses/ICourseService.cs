using Team21.Acc.Core.Common;

namespace Team21.Acc.Core.Courses;

/// <summary>Creates and archives courses for the current caller.</summary>
public interface ICourseService
{
    Task<Result<CourseDto>> CreateAsync(
        CreateCourseRequest request,
        CancellationToken cancellationToken = default
    );

    Task<Result> ArchiveAsync(
        Guid courseId,
        CancellationToken cancellationToken = default
    );
}