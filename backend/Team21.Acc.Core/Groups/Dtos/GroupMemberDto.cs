using System;
using System.Collections.Generic;
using System.Text;

namespace Team21.Acc.Core.Groups.Dtos;

public sealed record GroupMemberDto(
    Guid Id,
    Guid GroupId,
    Guid UserId,
    DateTime JoinedAt
);
