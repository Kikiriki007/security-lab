using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Platform.Access.AspNet;

namespace Platform.Access.Tests.AspNet;

[TestFixture]
[Property("RequirementId", "ACC-05")]
public class ApiEndpointCoverageTests
{
    private static readonly HashSet<string> ApprovedPublicRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/health",
        "/api/access/demo-deny",   
    };

    [Test]
    public void ACC05_Every_real_route_is_protected_or_approved_public()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var endpoints = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(s => s.Endpoints).OfType<RouteEndpoint>().ToList();

        Assert.That(endpoints, Is.Not.Empty);

        var violations = new List<string>();
        foreach (var e in endpoints)
        {
            var route = "/" + e.RoutePattern.RawText?.TrimStart('/');
            var hasReq = e.Metadata.GetMetadata<IAccessRequirementMetadata>() is not null;
            var isPublic = e.Metadata.GetMetadata<PublicEndpointMetadata>() is not null
                           || e.Metadata.GetMetadata<AllowPublicAttribute>() is not null;

            if (hasReq && isPublic) violations.Add($"{route}: both RequireAccess and AllowPublic");
            else if (!hasReq && !isPublic) violations.Add($"{route}: no access metadata");
            else if (isPublic && !ApprovedPublicRoutes.Contains(route)) violations.Add($"{route}: public but not approved");
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    [Test]
    public async Task ACC05_Health_is_public_and_protected_lab_route_is_denied_without_membership()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        Assert.That((await client.GetAsync("/api/health")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var res = await client.GetAsync($"/api/laboratories/{Guid.NewGuid()}");
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await res.Content.ReadAsStringAsync(), Does.Not.Contain("laboratory"));

        Assert.That((await client.GetAsync("/api/test/no-metadata")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
