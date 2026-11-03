
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Platform.Authoring.Application.Models;
using Platform.Authoring.Domain;
using Platform.Authoring.Infrastructure.Persistence;

namespace Platform.Authoring.Tests.Infrastructure;

[TestFixture]
[Category("Integration")]
public class LabPrerequisiteIntegrationTests
{
    private static AuthoringDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__Authoring");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__Authoring is missing.");
        }

        var options = new DbContextOptionsBuilder<AuthoringDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AuthoringDbContext(options);
    }

    private static LaboratoryData CreateLaboratory(
        Guid id, string title)
    {
        return new LaboratoryData(
            id,
            title,
            "Test laboratory",
            "Beginner",
            Enum.GetValues<LabType>()[0],
            new List<string> { "test" }
        );
    }

    [Test]
    public async Task Prerequisite_ShouldPersistAndBeRemovable()
    {
        var labAId = Guid.NewGuid();
        var labBId = Guid.NewGuid();

        try
        {
            // 1. Create two laboratories
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                await repository.AddAsync(
                    CreateLaboratory(labAId, "Lab A"));

                await repository.AddAsync(
                    CreateLaboratory(labBId, "Lab B"));

                // Lab B requires Lab A
                await repository.AddPrerequisiteAsync(
                    labBId, labAId);
            }

            // 2. Read using a fresh DbContext
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                var prerequisites =
                    await repository.GetPrerequisiteIdsAsync(labBId);

                Assert.That(
                    prerequisites,
                    Is.EquivalentTo(new[] { labAId }));

                var laboratory =
                    await repository.GetByIdAsync(labBId);

                Assert.That(laboratory, Is.Not.Null);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        laboratory!.Id,
                        Is.EqualTo(labBId));

                    Assert.That(
                        laboratory.Title,
                        Is.EqualTo("Lab B"));
                });
            }

            // 3. Remove prerequisite
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                await repository.RemovePrerequisiteAsync(
                    labBId, labAId);
            }

            // 4. Confirm removal with another DbContext
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                var prerequisites =
                    await repository.GetPrerequisiteIdsAsync(labBId);

                Assert.That(prerequisites, Is.Empty);
            }
        }
        finally
        {
            await using var context = CreateContext();

            // Remove relationships first
            await context.LabPrerequisites
                .Where(p =>
                    p.LaboratoryId == labAId ||
                    p.LaboratoryId == labBId ||
                    p.PrerequisiteLabId == labAId ||
                    p.PrerequisiteLabId == labBId)
                .ExecuteDeleteAsync();

            // Remove test laboratories
            await context.Laboratories
                .Where(l =>
                    l.Id == labAId ||
                    l.Id == labBId)
                .ExecuteDeleteAsync();
        }
    }

    [Test]
    public async Task Prerequisite_CannotReferenceItself()
    {
        var labId = Guid.NewGuid();

        try
        {
            await using var context = CreateContext();
            var repository = new LaboratoryRepository(context);

            await repository.AddAsync(
                CreateLaboratory(labId, "Self Reference Test"));

            Assert.ThrowsAsync<ArgumentException>(
                async () =>
                    await repository.AddPrerequisiteAsync(
                        labId, labId));

            var prerequisites =
                await repository.GetPrerequisiteIdsAsync(labId);

            Assert.That(prerequisites, Is.Empty);
        }
        finally
        {
            await using var context = CreateContext();

            await context.Laboratories
                .Where(l => l.Id == labId)
                .ExecuteDeleteAsync();
        }
    }

    [Test]
    public async Task DuplicatePrerequisite_ShouldBeRejected()
    {
        var labAId = Guid.NewGuid();
        var labBId = Guid.NewGuid();

        try
        {
            // 1. Create laboratories and the first relationship
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                await repository.AddAsync(
                    CreateLaboratory(labAId, "Lab A"));

                await repository.AddAsync(
                    CreateLaboratory(labBId, "Lab B"));

                await repository.AddPrerequisiteAsync(
                    labBId, labAId);
            }

            // 2. Attempt to insert the same relationship
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                Assert.ThrowsAsync<DbUpdateException>(
                    async () =>
                        await repository.AddPrerequisiteAsync(
                            labBId, labAId));
            }

            // 3. Check that only one relationship exists
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                var prerequisites =
                    await repository.GetPrerequisiteIdsAsync(labBId);

                Assert.That(
                    prerequisites,
                    Is.EquivalentTo(new[] { labAId }));
            }
        }
        finally
        {
            await using var context = CreateContext();

            await context.LabPrerequisites
                .Where(p =>
                    p.LaboratoryId == labAId ||
                    p.LaboratoryId == labBId ||
                    p.PrerequisiteLabId == labAId ||
                    p.PrerequisiteLabId == labBId)
                .ExecuteDeleteAsync();

            await context.Laboratories
                .Where(l =>
                    l.Id == labAId ||
                    l.Id == labBId)
                .ExecuteDeleteAsync();
        }
    }

    [Test]
    public async Task NonexistentPrerequisite_ShouldBeRejected()
    {
        var labId = Guid.NewGuid();
        var nonexistentId = Guid.NewGuid();

        try
        {
            // 1. Create an existing laboratory
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                await repository.AddAsync(
                    CreateLaboratory(labId, "Test Lab"));
            }

            // 2. Try to reference a nonexistent laboratory
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                Assert.ThrowsAsync<DbUpdateException>(
                    async () =>
                        await repository.AddPrerequisiteAsync(
                            labId, nonexistentId));
            }

            // 3. Verify nothing was stored
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                var prerequisites =
                    await repository.GetPrerequisiteIdsAsync(labId);

                Assert.That(prerequisites, Is.Empty);
            }
        }
        finally
        {
            await using var context = CreateContext();

            await context.LabPrerequisites
                .Where(p =>
                    p.LaboratoryId == labId ||
                    p.PrerequisiteLabId == labId)
                .ExecuteDeleteAsync();

            await context.Laboratories
                .Where(l => l.Id == labId)
                .ExecuteDeleteAsync();
        }
    }
}
