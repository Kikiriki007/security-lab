using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Platform.Audit.Tests;

// The real Platform.Api host must start with strict DI validation (host level).
// The Development environment enables ValidateScopes and ValidateOnBuild by default.
internal sealed class StrictHostFactory(bool addCaptiveDependency = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        if (addCaptiveDependency)
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ScopedDependency>();
                services.AddSingleton<SingletonConsumer>();
            });
        }
    }
}

[TestFixture]
public sealed class HostStartupScopeTests
{
    [Test]
    public async Task Host_StartsWithScopeValidation_AndHealthResponds()
    {
        using var factory = new StrictHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public void Host_WithCaptiveDependency_FailsToStart()
    {
        var exception = Assert.Catch<Exception>(() =>
        {
            using var factory = new StrictHostFactory(addCaptiveDependency: true);
            using var client = factory.CreateClient();
        });

        Assert.That(exception?.ToString(), Does.Contain("scoped").IgnoreCase);
    }
}
