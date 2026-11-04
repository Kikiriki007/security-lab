namespace Team21.Acc.Core.Courses;

public sealed record CourseDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Status,
    DateTime CreatedAt,
    DateTime? ArchivedAt
);