using System;
using System.Collections.Generic;
using System.Text;

namespace Platform.Assignments.Contracts;

public interface ILaboratoryPrerequisiteAccess
{
    ValueTask<bool> CanStartAsync(
        string studentId,
        string publishedLabRevisionId,
        CancellationToken cancellationToken = default);
}
