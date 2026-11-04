using Platform.Assignments.Contracts;
using Platform.Assignments.Domain.Models;
using Platform.Assignments.Domain.Enums;

namespace Platform.Assignments.Application;

public sealed class StartAttemptService
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IAttemptStore _attemptStore;

    public StartAttemptService(
    IAssignmentRepository assignmentRepository,
    IAttemptStore attemptStore)
    {
        _assignmentRepository = assignmentRepository;
        _attemptStore = attemptStore;
    }

    private async ValueTask<Assignment?> LoadAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        return await _assignmentRepository.GetByIdAsync(
            assignmentId,
            cancellationToken);
    }

    private static bool IsAssignmentAvailable(
    Assignment assignment,
    DateTimeOffset now)
    {
        return assignment.GetAvailability(now)
            == AssignmentAvailability.Available;
    }

    
    public async ValueTask<Guid> StartAttemptAsync(
        Guid assignmentId,
        string studentId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await LoadAssignmentAsync(
            assignmentId, cancellationToken);

        if (assignment == null)
        {
            throw new KeyNotFoundException("Assignment not found.");
        }

        if (!IsAssignmentAvailable(assignment, DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException("Assignment is not available.");
        }

        var attemptId = await _attemptStore.TryStartAsync(
            assignment.Id,
            studentId,
            assignment.PublishedLabRevisionId,
            assignment.MaxAttempts,
            requestId,
            cancellationToken);

        if (attemptId == null)
        {
            throw new InvalidOperationException("Maximum number of attempts reached.");
        }

        return attemptId.Value;
    }

}