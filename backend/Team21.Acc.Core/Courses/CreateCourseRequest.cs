namespace Team21.Acc.Core.Courses;

public sealed record CreateCourseRequest(
    string? Code,
    string? Name,
    string? Description
);