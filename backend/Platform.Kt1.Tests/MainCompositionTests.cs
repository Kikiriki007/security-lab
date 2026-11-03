using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Platform.Api.Modules.Kt1;
using MainAccountRepository = Platform.Api.Services.Identity.IAccountRepository;
using MainAccountStatus = Platform.Api.Domain.Identity.AccountStatus;

namespace Platform.Kt1.Tests;

[TestFixture]
public class MainCompositionTests
{
    private static readonly Guid MainAdmin = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MainTarget = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Test]
    public async Task EnablingKt1PreservesMainAccountStatusEndpointAndItsAuthorization()
    {
        using var factory = new Kt1Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Kt1-Demo", "1");
        Assert.That((await client.PostAsync("/api/kt1/session/admin", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var repository = factory.Services.GetRequiredService<MainAccountRepository>();
        var initialStatus = repository.Find(MainTarget)!.Status;
        var path = $"/api/admin/accounts/{MainTarget}/status";
        // A synthetic course-admin cookie is not an identity-module administrator session.
        Assert.That((await client.PutAsJsonAsync(path, new { status = "BLOCKED", reason = "Integration check" })).StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(repository.Find(MainTarget)!.Status, Is.EqualTo(initialStatus));
        // Keep main's explicitly configured Development stub and controller functional.
        client.DefaultRequestHeaders.Add("X-Stub-User", MainAdmin.ToString());
        Assert.That((await client.PutAsJsonAsync(path, new { status = "BLOCKED", reason = "Integration check" })).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));
        Assert.That(repository.Find(MainTarget)!.Status, Is.EqualTo(MainAccountStatus.Blocked));
        Assert.That(factory.Services.GetRequiredService<DemoStore>().FindAccount(DemoStore.Ana).Value!.IsActive, Is.True);
    }

    [Test]
    public async Task MainDevelopmentIdentityHeaderDoesNotAuthenticateOrElevateKt1Caller()
    {
        using var factory = new Kt1Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Stub-User", MainAdmin.ToString());
        Assert.That((await client.GetAsync($"/api/kt1/courses/{DemoStore.Course}/administration")).StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized));
        client.DefaultRequestHeaders.Add("X-Kt1-Demo", "1");
        Assert.That((await client.PostAsync("/api/kt1/session/ana", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await client.GetAsync($"/api/kt1/attempts/{DemoStore.MarkoAttempt}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Kt1AdminCookieDoesNotGrantMainAccountRegistrationPermission()
    {
        using var factory = new Kt1Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Kt1-Demo", "1");
        Assert.That((await client.PostAsync("/api/kt1/session/admin", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await client.PostAsJsonAsync("/api/admin/accounts", new
        {
            email = "integration@example.invalid",
            password = "Synthetic!Password42"
        })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
