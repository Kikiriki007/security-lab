
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Platform.Authoring.Application.Models;
using Platform.Authoring.Domain;
using Platform.Authoring.Infrastructure.Persistence;

namespace Platform.Authoring.Tests.Infrastructure;

[TestFixture]
[Category("Integration")]
public class LaboratoryRepositoryIntegrationTests
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

    [Test]
    public async Task Laboratory_ShouldPersistAcrossContexts()
    {
        var id = Guid.NewGuid();

        try
        {
            // 1. Create and save laboratory
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                await repository.AddAsync(new LaboratoryData(
                    id,
                    "Network Security",
                    "Learn basic network security",
                    "Beginner",
                    Enum.GetValues<LabType>()[0],
                    new List<string>
                    {
                        "network",
                        "security"
                    }
                ));
            }

            // 2. Load using a new DbContext
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);
                var laboratory = await repository.GetByIdAsync(id);

                Assert.That(laboratory, Is.Not.Null);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        laboratory!.Id,
                        Is.EqualTo(id));

                    Assert.That(
                        laboratory.Title,
                        Is.EqualTo("Network Security"));

                    Assert.That(
                        laboratory.Goal,
                        Is.EqualTo("Learn basic network security"));

                    Assert.That(
                        laboratory.Difficulty,
                        Is.EqualTo("Beginner"));

                    Assert.That(
                        laboratory.Tags,
                        Does.Contain("network"));

                    Assert.That(
                        laboratory.Tags,
                        Does.Contain("security"));
                });
            }
        }
        finally
        {
            // Clean up test data
            await using var context = CreateContext();

            await context.Laboratories
                .Where(l => l.Id == id)
                .ExecuteDeleteAsync();
        }
    }

    [Test]
    public async Task Update_ShouldPersistWithoutChangingId()
    {
        var id = Guid.NewGuid();

        try
        {
            // 1. Create initial laboratory
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                await repository.AddAsync(new LaboratoryData(
                    id,
                    "Original Title",
                    "Original Goal",
                    "Beginner",
                    Enum.GetValues<LabType>()[0],
                    new List<string>
                    {
                        "original"
                    }
                ));
            }

            // 2. Update using a different DbContext
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);

                await repository.UpdateAsync(new LaboratoryData(
                    id,
                    "Updated Title",
                    "Updated Goal",
                    "Advanced",
                    Enum.GetValues<LabType>()[0],
                    new List<string>
                    {
                        "updated"
                    }
                ));
            }

            // 3. Verify persisted changes
            await using (var context = CreateContext())
            {
                var repository = new LaboratoryRepository(context);
                var updated = await repository.GetByIdAsync(id);

                Assert.That(updated, Is.Not.Null);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        updated!.Id,
                        Is.EqualTo(id));

                    Assert.That(
                        updated.Title,
                        Is.EqualTo("Updated Title"));

                    Assert.That(
                        updated.Goal,
                        Is.EqualTo("Updated Goal"));

                    Assert.That(
                        updated.Difficulty,
                        Is.EqualTo("Advanced"));

                    Assert.That(
                        updated.Tags,
                        Does.Contain("updated"));

                    Assert.That(
                        updated.Tags,
                        Does.Not.Contain("original"));
                });
            }
        }
        finally
        {
            // Clean up test data
            await using var context = CreateContext();

            await context.Laboratories
                .Where(l => l.Id == id)
                .ExecuteDeleteAsync();
        }
    }
}
