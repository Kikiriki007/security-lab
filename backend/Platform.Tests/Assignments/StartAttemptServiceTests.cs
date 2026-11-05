using NUnit.Framework;
using Platform.Assignments.Application;
using Platform.Assignments.Contracts;
using Platform.Assignments.Domain.Models;

namespace Platform.Tests.Assignments;

[TestFixture]
public class StartAttemptServiceTests
{
    [Test]
    public void StartAttempt_WhenLimitReached_Throws()
    {
        var assignment = CreateAssignment(maxAttempts: 2);
        var attemptStore = new FakeAttemptStore(2);
        var service = CreateService(assignment, attemptStore);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.StartAttemptAsync(
                assignment.Id, "student-1", Guid.NewGuid()));

        Assert.That(exception!.Message,
            Is.EqualTo("Maximum number of attempts reached."));
    }

    [Test]
    public async Task StartAttempt_WhenLimitNotReached_Succeeds()
    {
        var assignment = CreateAssignment(maxAttempts: 3);
        var attemptStore = new FakeAttemptStore(1);
        var service = CreateService(assignment, attemptStore);

        var attemptId = await service.StartAttemptAsync(
            assignment.Id, "student-1", Guid.NewGuid());

        Assert.That(attemptId, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void StartAttempt_WhenAssignmentNotFound_Throws()
    {
        var assignment = CreateAssignment();
        var attemptStore = new FakeAttemptStore(0);
        var service = CreateService(assignment, attemptStore);

        var exception = Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await service.StartAttemptAsync(
                Guid.NewGuid(), "student-1", Guid.NewGuid()));

        Assert.That(exception!.Message, Is.EqualTo("Assignment not found."));
    }

    [Test]
    public void StartAttempt_WhenDeadlineExpired_Throws()
    {
        var assignment = CreateAssignment(
            dueAtUtc: DateTimeOffset.UtcNow.AddDays(-1));

        var attemptStore = new FakeAttemptStore(0);
        var service = CreateService(assignment, attemptStore);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.StartAttemptAsync(
                assignment.Id, "student-1", Guid.NewGuid()));

        Assert.That(exception!.Message,
            Is.EqualTo("Assignment is not available."));
    }

    [Test]
    public void StartAttempt_WhenAssignmentArchived_Throws()
    {
        var assignment = CreateAssignment();
        assignment.Archive();

        var attemptStore = new FakeAttemptStore(0);
        var service = CreateService(assignment, attemptStore);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.StartAttemptAsync(
                assignment.Id, "student-1", Guid.NewGuid()));

        Assert.That(exception!.Message,
            Is.EqualTo("Assignment is not available."));
    }

    [Test]
    public async Task StartAttempt_WhenRequestRepeated_ReturnsSameAttempt()
    {
        var assignment = CreateAssignment(maxAttempts: 1);
        var attemptStore = new FakeAttemptStore(0);
        var service = CreateService(assignment, attemptStore);
        var requestId = Guid.NewGuid();

        var firstAttempt = await service.StartAttemptAsync(
            assignment.Id, "student-1", requestId);

        var secondAttempt = await service.StartAttemptAsync(
            assignment.Id, "student-1", requestId);

        Assert.That(secondAttempt, Is.EqualTo(firstAttempt));
        Assert.That(attemptStore.AttemptCount, Is.EqualTo(1));
    }

    [Test]
    public void StartAttempt_WhenAccessDenied_Throws()
    {
        var assignment = CreateAssignment();
        var attemptStore = new FakeAttemptStore(0);

        var service = CreateService(
            assignment,
            attemptStore,
            authorizationAllowed: false);

        var exception = Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await service.StartAttemptAsync(
                assignment.Id, "student-1", Guid.NewGuid()));

        Assert.That(exception!.Message, Is.EqualTo("Access denied."));
        Assert.That(attemptStore.AttemptCount, Is.EqualTo(0));
    }

    [Test]
    public void StartAttempt_WhenPrerequisitesNotFulfilled_Throws()
    {
        var assignment = CreateAssignment();
        var attemptStore = new FakeAttemptStore(0);

        var service = CreateService(
            assignment,
            attemptStore,
            prerequisitesFulfilled: false);

        var exception = Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await service.StartAttemptAsync(
                assignment.Id, "student-1", Guid.NewGuid()));

        Assert.That(
            exception!.Message,
            Is.EqualTo("Laboratory prerequisites are not fulfilled."));

        Assert.That(attemptStore.AttemptCount, Is.EqualTo(0));
    }

    [Test]
    public async Task StartAttempt_WhenPrerequisitesFulfilled_Succeeds()
    {
        var assignment = CreateAssignment();
        var attemptStore = new FakeAttemptStore(0);

        var service = CreateService(
            assignment,
            attemptStore,
            prerequisitesFulfilled: true);

        var attemptId = await service.StartAttemptAsync(
            assignment.Id, "student-1", Guid.NewGuid());

        Assert.That(attemptId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(attemptStore.AttemptCount, Is.EqualTo(1));
    }

    private static Assignment CreateAssignment(
        int maxAttempts = 3,
        DateTimeOffset? dueAtUtc = null)
    {
        return new Assignment(
            Guid.NewGuid(),
            "course-oib",
            "revision-1",
            null,
            dueAtUtc ?? DateTimeOffset.UtcNow.AddDays(1),
            maxAttempts);
    }

    private static StartAttemptService CreateService(
        Assignment assignment,
        FakeAttemptStore attemptStore,
        bool authorizationAllowed = true,
        bool prerequisitesFulfilled = true)
    {
        return new StartAttemptService(
            new FakeAssignmentRepository(assignment),
            attemptStore,
            new FakeStartAttemptAuthorization(authorizationAllowed),
            new FakeLaboratoryPrerequisiteAccess(prerequisitesFulfilled));
    }

    private sealed class FakeAssignmentRepository(
        Assignment assignment) : IAssignmentRepository
    {
        public ValueTask<Assignment?> GetByIdAsync(
            Guid assignmentId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<Assignment?>(
                assignmentId == assignment.Id ? assignment : null);
        }

        public ValueTask<IReadOnlyList<Assignment>> GetForCoursesAsync(
            IReadOnlyCollection<string> courseIds,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask AddAsync(
            Assignment assignment,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask UpdateAsync(
            Assignment assignment,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeAttemptStore(
        int existingAttempts) : IAttemptStore
    {
        private int _attemptCount = existingAttempts;

        private readonly Dictionary<(Guid, string, Guid), Guid> _requests = new();

        public int AttemptCount => _attemptCount;

        public ValueTask<Guid?> TryStartAsync(
            Guid assignmentId,
            string studentId,
            string publishedLabRevisionId,
            int maxAttempts,
            Guid requestId,
            CancellationToken cancellationToken = default)
        {
            var key = (assignmentId, studentId, requestId);

            if (_requests.TryGetValue(key, out var existingId))
            {
                return ValueTask.FromResult<Guid?>(existingId);
            }

            if (_attemptCount >= maxAttempts)
            {
                return ValueTask.FromResult<Guid?>(null);
            }

            var attemptId = Guid.NewGuid();
            _requests.Add(key, attemptId);
            _attemptCount++;

            return ValueTask.FromResult<Guid?>(attemptId);
        }
    }

    private sealed class FakeStartAttemptAuthorization(bool allow)
        : IStartAttemptAuthorization
    {
        public ValueTask<bool> CanStartAsync(
            string studentId,
            Guid assignmentId,
            string courseId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(allow);
        }
    }

    private sealed class FakeLaboratoryPrerequisiteAccess(bool allow)
        : ILaboratoryPrerequisiteAccess
    {
        public ValueTask<bool> CanStartAsync(
            string studentId,
            string publishedLabRevisionId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(allow);
        }
    }
}