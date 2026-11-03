using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Access.Infrastructure.Catalog;
using Platform.Api.Domain.Authentication;
using Platform.Api.Security;
using Platform.Api.Services.Identity;
using PlatformAccessDecision = Platform.Access.Contracts.IAccessDecision;

namespace Platform.Tests.Security;

// ACC-05 fallback-deny, part owned by DM: no development fake may be active outside Development.
// These are tests only; they run against the real Platform.Api host.
[TestFixture]
public sealed class NoFakesOutsideDevelopmentTests
{
    private const string AdminId = "11111111-1111-1111-1111-111111111111";
    private const string StudentId = "22222222-2222-2222-2222-222222222222";

    private static readonly string[] FakeMarkers = ["Stub", "Fake", "AllowAll", "AlwaysAllow", "PermitAll", "AlwaysActive", "Development"];

    private sealed class EnvironmentHost(string environment, bool devConfigurationLeak = false) : WebApplicationFactory<Program>
    {
        public IReadOnlyList<ServiceDescriptor> Descriptors { get; private set; } = [];

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.ClearProviders());

            if (devConfigurationLeak)
            {
                // Settings that make a Development host trust a caller must have no effect elsewhere.
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Identity:DevSubjectId"] = AdminId,
                        ["Identity:AdminAccountIds:0"] = AdminId,
                        ["Stub:AdminIds"] = AdminId,
                    }));
            }

            builder.ConfigureTestServices(services => Descriptors = [.. services]);
        }
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, string? stubUser, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (stubUser is not null)
        {
            request.Headers.Add("X-Stub-User", stubUser);
        }

        return await client.SendAsync(request);
    }

    // The control: the same checks must find the fakes in Development, otherwise "not found" proves nothing.
    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Control_DevelopmentHost_DoesUseTheDevelopmentFakes()
    {
        using var host = new EnvironmentHost("Development");
        using var client = host.CreateClient();
        using var scope = host.Services.CreateScope();

        Assert.That(scope.ServiceProvider.GetRequiredService<IRequestSubjectResolver>(), Is.TypeOf<DevelopmentSubjectResolver>());
        Assert.That(scope.ServiceProvider.GetRequiredService<IIdentityContext>(), Is.TypeOf<StubIdentityContext>());
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_NonDevelopment_UsesNoDevelopmentFakes(string environment)
    {
        using var host = new EnvironmentHost(environment);
        using var client = host.CreateClient();
        using var scope = host.Services.CreateScope();

        Assert.That(scope.ServiceProvider.GetRequiredService<IRequestSubjectResolver>(), Is.TypeOf<UnauthenticatedSubjectResolver>());
        Assert.That(scope.ServiceProvider.GetRequiredService<IIdentityContext>(), Is.TypeOf<AnonymousIdentityContext>());
        Assert.That(scope.ServiceProvider.GetRequiredService<ISessionManager>(), Is.TypeOf<NoOpSessionRevoker>());
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_NonDevelopment_NoRegisteredImplementationLooksLikeAFake(string environment)
    {
        using var host = new EnvironmentHost(environment);
        using var client = host.CreateClient();

        var suspicious = host.Descriptors
            .Select(d => d.ImplementationType ?? d.ImplementationInstance?.GetType())
            .Where(t => t is not null && t.Namespace is not null && t.Namespace.StartsWith("Platform.", StringComparison.Ordinal))
            .Where(t => FakeMarkers.Any(marker => t!.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t!.FullName)
            .ToList();

        Assert.That(suspicious, Is.Empty);
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_DevelopmentHost_ControlFindsTheFakesByName()
    {
        using var host = new EnvironmentHost("Development");
        using var client = host.CreateClient();

        var suspicious = host.Descriptors
            .Select(d => d.ImplementationType ?? d.ImplementationInstance?.GetType())
            .Where(t => t is not null && t.Namespace is not null && t.Namespace.StartsWith("Platform.", StringComparison.Ordinal))
            .Where(t => FakeMarkers.Any(marker => t!.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.That(suspicious, Is.Not.Empty, "the name scan must be able to see the Development fakes");
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_NonDevelopment_DemoDenyEndpoint_DoesNotExist(string environment)
    {
        using var host = new EnvironmentHost(environment);
        using var client = host.CreateClient();

        using var response = await Send(client, HttpMethod.Get, "/api/access/demo-deny", stubUser: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_Control_DevelopmentHost_DemoDenyEndpointExists()
    {
        using var host = new EnvironmentHost("Development");
        using var client = host.CreateClient();

        using var response = await Send(client, HttpMethod.Get, "/api/access/demo-deny", stubUser: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_Production_StubUserHeader_GivesNoIdentity()
    {
        using var host = new EnvironmentHost("Production");
        using var client = host.CreateClient();

        using var response = await Send(
            client, HttpMethod.Put, $"/api/admin/accounts/{StudentId}/status", stubUser: AdminId, body: new { status = "BLOCKED" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_Production_DevelopmentSettings_DoNotGrantAnyone()
    {
        using var host = new EnvironmentHost("Production", devConfigurationLeak: true);
        using var client = host.CreateClient();

        using var statusChange = await Send(
            client, HttpMethod.Put, $"/api/admin/accounts/{StudentId}/status", stubUser: AdminId, body: new { status = "BLOCKED" });
        using var createAccount = await Send(
            client, HttpMethod.Post, "/api/admin/accounts", stubUser: AdminId, body: new { email = "", password = "" });

        Assert.That(statusChange.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(createAccount.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_Control_DevelopmentHost_SameCreateAccountRequest_PassesTheAccessCheck()
    {
        // With the Development settings the very same request does pass the access check, so the 401 above
        // really comes from the environment and not from a broken request.
        using var host = new EnvironmentHost("Development", devConfigurationLeak: true);
        using var client = host.CreateClient();

        using var response = await Send(
            client, HttpMethod.Post, "/api/admin/accounts", stubUser: null, body: new { email = "", password = "" });

        // Empty values: the request passes the access check and is then rejected by input validation,
        // so nothing is ever stored.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Production_NoSeededTestAccounts()
    {
        using var host = new EnvironmentHost("Production");
        using var client = host.CreateClient();

        var accounts = host.Services.GetRequiredService<IAccountRepository>();

        Assert.That(accounts.Find(Guid.Parse(AdminId)), Is.Null);
        Assert.That(accounts.Find(Guid.Parse(StudentId)), Is.Null);
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Production_ExactlyOneAccessDecision_TheDefaultDenyEngine()
    {
        using var host = new EnvironmentHost("Production");
        using var client = host.CreateClient();

        var registrations = host.Descriptors.Where(d => d.ServiceType == typeof(PlatformAccessDecision)).ToList();

        Assert.That(registrations, Has.Count.EqualTo(1));
        Assert.That(registrations[0].ImplementationType?.Name, Is.EqualTo("DefaultDenyAccessDecision"));
        Assert.That(host.Services.GetRequiredService<PlatformAccessDecision>().GetType().Name, Is.EqualTo("DefaultDenyAccessDecision"));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_NoTypeInThePlatform_OtherThanTheEngine_ImplementsTheAccessDecision()
    {
        var implementations = PlatformAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(PlatformAccessDecision).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.That(implementations, Is.EqualTo(new[] { "DefaultDenyAccessDecision" }));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_NoAllowAllTypeInThePlatform()
    {
        string[] banned = ["AllowAll", "AlwaysAllow", "PermitAll", "AllowEverything"];

        var found = PlatformAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => banned.Any(b => t.Name.Contains(b, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.FullName)
            .ToList();

        Assert.That(found, Is.Empty);
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_NonDevelopment_UnknownActionOrNoSubject_IsDenied(string environment)
    {
        using var host = new EnvironmentHost(environment);
        using var client = host.CreateClient();
        var engine = host.Services.GetRequiredService<PlatformAccessDecision>();

        var unknown = await engine.DecideAsync(
            new AccessRequest(new SubjectRef(AdminId, AccountState.Active), "never.registered.action", null, "c1"));
        var anonymous = await engine.DecideAsync(new AccessRequest(null, "account.status.change", null, "c2"));

        Assert.That(unknown.DenialReason, Is.EqualTo(AccessDenialReason.UnknownAction));
        Assert.That(anonymous.DenialReason, Is.EqualTo(AccessDenialReason.Unauthenticated));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_Production_RegisteredActionWithoutPolicy_IsDenied()
    {
        using var host = new EnvironmentHost("Production");
        using var client = host.CreateClient();
        host.Services.GetRequiredService<InMemoryActionCatalog>().Register("probe.without.policy");
        var engine = host.Services.GetRequiredService<PlatformAccessDecision>();

        var decision = await engine.DecideAsync(
            new AccessRequest(new SubjectRef(AdminId, AccountState.Active), "probe.without.policy", null, "c1"));

        Assert.That(decision.DenialReason, Is.EqualTo(AccessDenialReason.NoPolicy));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Production_AuditWriterIsTheResilientChain_NotTheNullWriter()
    {
        using var host = new EnvironmentHost("Production");
        using var client = host.CreateClient();

        var writer = host.Services.GetRequiredService<Platform.Access.Contracts.IAccessAuditWriter>();

        Assert.That(writer.GetType().Name, Is.EqualTo("ResilientAccessAuditWriter"));
    }

    private static IEnumerable<System.Reflection.Assembly> PlatformAssemblies() =>
    [
        typeof(Program).Assembly,
        typeof(PlatformAccessDecision).Assembly,
        typeof(Platform.Identity.Composition.IdentityAccessRegistration).Assembly,
        typeof(Platform.Audit.Composition.AuditServiceCollectionExtensions).Assembly,
    ];
}
