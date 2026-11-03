
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Platform.Authoring.Infrastructure.Persistence;

namespace Platform.Authoring.Tests.Infrastructure;

[TestFixture]
[Category("Integration")]
public class AuthoringDbContextConnectionTests
{
    [Test]
    public async Task CanConnect_ToPostgreSql()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__Authoring");

        Assert.That(
            connectionString,
            Is.Not.Null.And.Not.Empty,
            "Set ConnectionStrings__Authoring before running this test.");

        var options = new DbContextOptionsBuilder<AuthoringDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new AuthoringDbContext(options);

        var canConnect = await context.Database.CanConnectAsync();

        Assert.That(canConnect, Is.True);
    }
}
