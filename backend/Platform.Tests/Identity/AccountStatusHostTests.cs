using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Access.Infrastructure.Catalog;
using Platform.Api.Services.Identity;

namespace Platform.Tests.Identity;

// Real Platform.Api host (Development): IDN-03 endpoint -> use-case -> adapter -> central access engine -> audit.
[TestFixture]
public sealed class AccountStatusHostTests
{
    private const string AdminId = "11111111-1111-1111-1111-111111111111";
    private const string StudentId = "22222222-2222-2222-2222-222222222222";
    private const string OtherId = "33333333-3333-3333-3333-333333333333";

    private sealed record LogLine(string Category, LogLevel Level, string EventName, string Message);

    private sealed class LogCollector : ILoggerProvider
    {
        private readonly List<LogLine> _lines = [];

        public IReadOnlyList<LogLine> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, _lines);

        public void Dispose()
        {
        }

        private sealed class Sink(string category, List<LogLine> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (lines)
                {
                    lines.Add(new LogLine(category, level, eventId.Name ?? "", formatter(state, exception)));
                }
            }
        }
    }

    private sealed class HostFactory(string? extraAdminIds = null) : WebApplicationFactory<Program>
    {
        public LogCollector Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddProvider(Logs);
            });

            if (extraAdminIds is not null)
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Stub:AdminIds"] = AdminId + "," + extraAdminIds,
                    }));
            }
        }
    }

    private static async Task<HttpResponseMessage> PutStatus(HttpClient client, string? caller, string target, string status)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/accounts/{target}/status")
        {
            Content = JsonContent.Create(new { status, reason = "test" }),
        };
        if (caller is not null)
        {
            request.Headers.Add("X-Stub-User", caller);
        }

        return await client.SendAsync(request);
    }

    private static IEnumerable<LogLine> AccessLines(HostFactory factory) =>
        factory.Logs.Lines.Where(l => l.EventName is "access.denied" or "access.allowed.privileged");

    [Test]
    [Property("RequirementId", "IDN-03")]
    public void IDN03_Host_UsesTheCentralEngine_NotTheOldAdminOnlyDecision()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();

        Assert.That(factory.Services.GetRequiredService<IAccessDecision>(), Is.TypeOf<PlatformAccessDecisionAdapter>());
        Assert.That(factory.Services.GetRequiredService<InMemoryActionCatalog>().Contains("account.status.change"), Is.True);
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Admin_ChangeStatus_Allowed_AndPrivilegedAudited()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();

        using var response = await PutStatus(client, AdminId, StudentId, "BLOCKED");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var line = AccessLines(factory).Single();
        Assert.That(line.EventName, Is.EqualTo("access.allowed.privileged"));
        Assert.That(line.Message, Does.Contain($"actor={AdminId}"));
        Assert.That(line.Message, Does.Contain("action=account.status.change"));
        Assert.That(line.Message, Does.Contain("policyId=identity.account.status.change.admin-only"));
        Assert.That(line.Message, Does.Not.Contain("correlationId=-"));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_NonAdmin_Forbidden_AndDenyAudited()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();

        using var response = await PutStatus(client, OtherId, StudentId, "BLOCKED");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var line = AccessLines(factory).Single();
        Assert.That(line.EventName, Is.EqualTo("access.denied"));
        Assert.That(line.Message, Does.Contain($"actor={OtherId}"));
        Assert.That(line.Message, Does.Contain("action=account.status.change"));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_NonAdmin_DoesNotChangeTheStatus()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();

        using var denied = await PutStatus(client, OtherId, StudentId, "BLOCKED");
        // The administrator now asks for the same change: it must still be a real transition, so the
        // earlier denied call did not apply it.
        using var allowed = await PutStatus(client, AdminId, StudentId, "BLOCKED");

        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_BlockedAdmin_Denied()
    {
        using var factory = new HostFactory(extraAdminIds: StudentId);
        using var client = factory.CreateClient();

        // The first administrator blocks the second one; the blocked administrator must lose the right.
        using var block = await PutStatus(client, AdminId, StudentId, "BLOCKED");
        using var attempt = await PutStatus(client, StudentId, OtherId, "BLOCKED");

        Assert.That(block.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(attempt.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var denied = AccessLines(factory).Single(l => l.EventName == "access.denied");
        Assert.That(denied.Message, Does.Contain("denialReason=account_not_active"));
        Assert.That(denied.Message, Does.Contain($"actor={StudentId}"));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_AdminWithoutAnAccount_Denied()
    {
        var ghost = Guid.NewGuid().ToString();
        using var factory = new HostFactory(extraAdminIds: ghost);
        using var client = factory.CreateClient();

        using var response = await PutStatus(client, ghost, StudentId, "BLOCKED");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(AccessLines(factory).Single().Message, Does.Contain("denialReason=account_not_active"));
    }

    [Test]
    [Property("RequirementId", "IDN-03")]
    public async Task IDN03_Anonymous_Unauthenticated()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();

        using var response = await PutStatus(client, caller: null, StudentId, "BLOCKED");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
