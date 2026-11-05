using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Api.Modules.Laboratories.Api;
using Platform.Api.Modules.Laboratories.Application;

namespace Laboratories.Tests;

// HTTP testovi LAB-01 API-ja nad stvarnim hostom u memoriji (WebApplicationFactory).
// Svaki test pravi svoj host, pa ima svoje prazno skladiste.
[TestFixture]
public sealed class LaboratoriesHttpTests
{
    private const string Route = "/api/laboratories/";
    private const string TestUserHeader = "X-Test-User";
    private const string Teacher = "marko:Teacher";
    private const string Student = "ana:Student";

    // ---------- Provera pristupa pre endpoint-a (kostur) ----------

    [TestCase("GET", null, HttpStatusCode.Unauthorized)]
    [TestCase("POST", null, HttpStatusCode.Unauthorized)]
    [TestCase("GET", Student, HttpStatusCode.OK)]
    [TestCase("POST", Student, HttpStatusCode.Forbidden)]
    [TestCase("GET", Teacher, HttpStatusCode.OK)]
    [TestCase("GET", "ana:Unknown", HttpStatusCode.Unauthorized)]
    [TestCase("GET", "invalid", HttpStatusCode.Unauthorized)]
    public async Task Development_EnforcesAccessBeforeEndpoint(
        string method, string? testUser, HttpStatusCode expectedStatus)
    {
        using var factory = new LaboratoriesHostFactory("Development");
        using var client = factory.CreateClient();
        using var request = CreateRequest(method, Route, testUser);

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
        if (expectedStatus == HttpStatusCode.OK)
        {
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("[]"));
        }
    }

    [Test]
    public async Task Development_WithMultipleIdentityHeaders_Denies()
    {
        using var factory = new LaboratoriesHostFactory("Development");
        using var client = factory.CreateClient();
        using var request = CreateRequest("POST", Route, null);
        request.Headers.Add(TestUserHeader, new[] { Student, Teacher });

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Development_IdentityDoesNotLeakBetweenRequests()
    {
        using var factory = new LaboratoriesHostFactory("Development");
        using var client = factory.CreateClient();

        using var teacherResponse = await client.SendAsync(CreateJsonRequest("POST", Route, Teacher, new { title = "Lab nastavnika" }));
        Assert.That(teacherResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var studentResponse = await client.SendAsync(CreateJsonRequest("POST", Route, Student, new { title = "Lab studenta" }));
        Assert.That(studentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        using var anonymousResponse = await client.SendAsync(CreateJsonRequest("POST", Route, null, new { title = "Anonimni lab" }));
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        Assert.That(await StoredCountAsync(factory), Is.EqualTo(1));
    }

    [TestCase("Production", "GET", "/api/laboratories/")]
    [TestCase("Production", "POST", "/api/laboratories/")]
    [TestCase("Staging", "GET", "/api/laboratories/")]
    [TestCase("Staging", "POST", "/api/laboratories/")]
    [TestCase("Staging", "POST", "/api/labs/")]
    public async Task OutsideDevelopment_TestTeacherHeaderDoesNotGrantAccess(
        string environment, string method, string path)
    {
        using var factory = new LaboratoriesHostFactory(environment);
        using var client = factory.CreateClient();
        using var request = CreateJsonRequest(method, path, Teacher, new { title = "Ne sme proci" });

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // ---------- POST: kreiranje ----------

    [Test]
    public async Task PostLabs_WhenTeacherCreates_Returns201WithServerIdAndLocation()
    {
        using var factory = new LaboratoriesHostFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(CreateJsonRequest("POST", Route, Teacher, new { title = "  RBAC i kontrola pristupa  " }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<LaboratoryResponse>();
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(created.Title, Is.EqualTo("RBAC i kontrola pristupa"));
            Assert.That(created.Status, Is.EqualTo("Draft"));
            Assert.That(created.CreatedBy, Is.Not.EqualTo(Guid.Empty));
            Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo($"/api/laboratories/{created.Id}"));
        });
    }

    [Test]
    public async Task PostLabs_WhenClientSendsOwnId_ServerIgnoresItAndAssignsNewId()
    {
        using var factory = new LaboratoriesHostFactory("Development");
        using var client = factory.CreateClient();
        Guid clientSuppliedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid clientSuppliedCreator = Guid.Parse("22222222-2222-2222-2222-222222222222");

        using var response = await client.SendAsync(CreateJsonRequest("POST", Route, Teacher, new
        {
            id = clientSuppliedId,
            title = "RBAC i kontrola pristupa",
            createdBy = clientSuppliedCreator,
            status = "Archived",
            createdAt = "2000-01-01T00:00:00Z",
        }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<LaboratoryResponse>();
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(created.Id, Is.Not.EqualTo(clientSuppliedId));
            Assert.That(created.CreatedBy, Is.Not.EqualTo(clientSuppliedCreator));
            Assert.That(created.Status, Is.EqualTo("Draft"));
            Assert.That(created.CreatedAt.Year, Is.Not.EqualTo(2000));
            Assert.That(created.Title, Is.EqualTo("RBAC i kontrola pristupa"));
        });

        // Klijentski ID nije zavrsio ni u skladistu.
        var repository = factory.Services.GetRequiredService<ILaboratoryRepository>();
        Assert.That(await repository.GetByIdAsync(clientSuppliedId), Is.Null);
        Assert.That(await repository.GetByIdAsync(created!.Id), Is.Not.Null);
    }

    [Test]
    public async Task PostLabs_TwoCreatesWithSameTitle_GetDifferentIds()
    {
        using var factory = new LaboratoriesHostFactory("Development");
        using var client = factory.CreateClient();

        LaboratoryResponse first = await CreateLabAsync(client, Teacher, "Isti naslov");
        LaboratoryResponse second = await CreateLabAsync(client, Teacher, "Isti naslov");

        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
    }

    [Test]
    public async Task PostLabs_ThroughAliasRoute_UsesSameChecks()
    {
        using var factory = new LaboratoriesHostFactory("Development");
        using var client = factory.CreateClient();

        using var teacher = await client.SendAsync(CreateJsonRequest("POST", "/api/labs", Teacher, new { title = "Preko aliasa" }));
        using var student = await client.SendAsync(CreateJsonRequest("POST", "/api/labs", Student, new { title = "Preko aliasa" }));

        Assert.Multiple(() =>
        {
            Assert.That(teacher.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(teacher.Headers.Location?.OriginalString, Does.StartWith("/api/laboratories/"));
            Assert.That(student.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    // ---------- Pomocne metode ----------

    private static async Task<LaboratoryResponse> CreateLabAsync(HttpClient client, string user, string title)
    {
        using var response = await client.SendAsync(CreateJsonRequest("POST", Route, user, new { title }));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<LaboratoryResponse>();
        Assert.That(created, Is.Not.Null);
        return created!;
    }

    private static async Task<int> StoredCountAsync(WebApplicationFactory<Program> factory) =>
        (await factory.Services.GetRequiredService<ILaboratoryRepository>().ListAsync()).Count;

    private static HttpRequestMessage CreateRequest(string method, string path, string? testUser)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (testUser is not null)
        {
            request.Headers.Add(TestUserHeader, testUser);
        }

        return request;
    }

    private static HttpRequestMessage CreateJsonRequest(string method, string path, string? testUser, object body)
    {
        HttpRequestMessage request = CreateRequest(method, path, testUser);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private sealed class LaboratoriesHostFactory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.ClearProviders());
        }
    }
}