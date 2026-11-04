using Microsoft.EntityFrameworkCore;
using Npgsql;
using Team21.Acc.Core.Common;
using Team21.Acc.Core.Courses;
using Team21.Acc.Core.Interfaces;
using Team21.Acc.Infrastructure.Courses.Data;

namespace Team21.Acc.Infrastructure.Courses.Services;

public sealed class CourseService : ICourseService
{
    private readonly ICoursesDbContext _context;
    private readonly ICurrentActor _actor;
    private readonly ICourseAuthorization _authorization;

    public CourseService(
        ICoursesDbContext context,
        ICurrentActor actor,
        ICourseAuthorization authorization)
    {
        _context = context;
        _actor = actor;
        _authorization = authorization;
    }

    public async Task<Result<CourseDto>> CreateAsync(
        CreateCourseRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Authentication
        if (!_actor.IsAuthenticated ||
            _actor.UserId is not Guid userId ||
            userId == Guid.Empty)
        {
            return Error.Unauthorized("Sign in first.");
        }

        // 2. Autorizacija
        if (!await _authorization.CanCreateAsync(userId, cancellationToken))
        {
            return Error.Forbidden("Not allowed to create courses.");
        }

        // 3. Validacija
        if (string.IsNullOrWhiteSpace(request.Code))
            return Error.Validation("Course code is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Error.Validation("Course name is required.");

        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var description = request.Description?.Trim() ?? string.Empty;

        if (code.Length > Course.MaxCodeLength)
            return Error.Validation("Course code is too long.");

        if (name.Length > Course.MaxNameLength)
            return Error.Validation("Course name is too long.");

        if (description.Length > Course.MaxDescriptionLength)
            return Error.Validation("Course description is too long.");

        // 4. Duplicate check
        var exists = await _context.Courses
            .AnyAsync(course => course.Code == code, cancellationToken);

        if (exists)
            return Error.Conflict("Course code already exists.");

        // 5. Create using Nikola's domain model
        var course = new Course(code, name, description, userId);

        _context.Courses.Add(course);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Courses_Code"
            })
        {
            return Error.Conflict("Course code already exists.");
        }

        // 6. Rispons
        return new CourseDto(
            course.Id,
            course.Code,
            course.Name,
            course.Description,
            course.Status.ToString(),
            course.CreatedAt,
            course.ArchivedAt
        );
    }

    public async Task<Result> ArchiveAsync(
        Guid courseId,
        CancellationToken cancellationToken = default)
    {
        // 1. Autentifikacija
        if (!_actor.IsAuthenticated ||
            _actor.UserId is not Guid userId ||
            userId == Guid.Empty)
        {
            return Error.Unauthorized("Sign in first.");
        }

        // 2. autorizacija
        if (!await _authorization.CanArchiveAsync(
                userId, courseId, cancellationToken))
        {
            return Error.Forbidden("Not allowed to archive this course.");
        }

        // 3. nadji kurs
        var course = await _context.Courses
            .SingleOrDefaultAsync(
                course => course.Id == courseId,
                cancellationToken);

        if (course is null)
            return Error.NotFound("Course not found.");

        if (course.Status == CourseStatus.Archived)
            return Result.Success();

        course.Archive(userId);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict(
                "Course was modified concurrently. Please retry.");
        }

        return Result.Success();
    }
}