using Platform.Api.Modules.Laboratories.Application;
using Platform.Api.Modules.Laboratories.Domain;
using Platform.Api.Modules.Laboratories.Infrastructure;
using Platform.Assignments.Contracts;
using Platform.Prerequisites.Contracts;
using Platform.Prerequisites.Domain;

namespace Laboratories.Tests;

[TestFixture]
public sealed class LaboratoryPrerequisiteAccessAdapterTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();

    private InMemoryLaboratoryRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryLaboratoryRepository();
    }

    [Test]
    public async Task CanStart_WhenPrerequisitesAreFulfilled_ReturnsTrue()
    {
        Laboratory laboratory = await AddLaboratoryAsync();
        LabRevision revision = await _repository.AddRevisionAsync(laboratory.Id);

        var prerequisiteService = new FakePrerequisiteAccessService(true);

        var adapter = new LaboratoryPrerequisiteAccessAdapter(
            _repository,
            prerequisiteService);

        bool result = await adapter.CanStartAsync(
            StudentId.ToString(),
            revision.Id.ToString());

        Assert.That(result, Is.True);
        Assert.That(prerequisiteService.LastStudentId, Is.EqualTo(StudentId));
        Assert.That(prerequisiteService.LastLaboratoryId, Is.EqualTo(laboratory.Id));
    }

    [Test]
    public async Task CanStart_WhenPrerequisitesAreNotFulfilled_ReturnsFalse()
    {
        Laboratory laboratory = await AddLaboratoryAsync();
        LabRevision revision = await _repository.AddRevisionAsync(laboratory.Id);

        var adapter = new LaboratoryPrerequisiteAccessAdapter(
            _repository,
            new FakePrerequisiteAccessService(false));

        bool result = await adapter.CanStartAsync(
            StudentId.ToString(),
            revision.Id.ToString());

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task CanStart_WhenRevisionDoesNotExist_ReturnsFalse()
    {
        var prerequisiteService = new FakePrerequisiteAccessService(true);

        var adapter = new LaboratoryPrerequisiteAccessAdapter(
            _repository,
            prerequisiteService);

        bool result = await adapter.CanStartAsync(
            StudentId.ToString(),
            Guid.NewGuid().ToString());

        Assert.That(result, Is.False);
        Assert.That(prerequisiteService.WasCalled, Is.False);
    }

    [Test]
    public async Task CanStart_WhenStudentIdIsInvalid_ReturnsFalse()
    {
        Laboratory laboratory = await AddLaboratoryAsync();
        LabRevision revision = await _repository.AddRevisionAsync(laboratory.Id);

        var prerequisiteService = new FakePrerequisiteAccessService(true);

        var adapter = new LaboratoryPrerequisiteAccessAdapter(
            _repository,
            prerequisiteService);

        bool result = await adapter.CanStartAsync(
            "invalid-student-id",
            revision.Id.ToString());

        Assert.That(result, Is.False);
        Assert.That(prerequisiteService.WasCalled, Is.False);
    }

    private async Task<Laboratory> AddLaboratoryAsync()
    {
        var laboratory = new Laboratory("SQL injection", TeacherId);
        await _repository.AddAsync(laboratory);
        return laboratory;
    }

    private sealed class FakePrerequisiteAccessService(bool unlocked)
        : IPrerequisiteAccessService
    {
        public Guid? LastStudentId { get; private set; }

        public Guid? LastLaboratoryId { get; private set; }

        public bool WasCalled { get; private set; }

        public Task<PrerequisiteAccessResult> CheckAccessAsync(
            Guid studentId,
            Guid laboratoryId,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastStudentId = studentId;
            LastLaboratoryId = laboratoryId;

            return Task.FromResult(
                new PrerequisiteAccessResult(
                    unlocked,
                    Array.Empty<Guid>()));
        }
    }
}