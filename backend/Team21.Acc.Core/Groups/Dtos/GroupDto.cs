using System;
using System.Collections.Generic;
using System.Text;


namespace Team21.Acc.Core.Groups.Dtos;

public sealed record GroupDto(
    Guid Id,
    Guid CourseId,
    string Name,
    GroupStatus Status
);
