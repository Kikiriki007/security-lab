using Platform.Access.Contracts;
using Platform.Access.Composition;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Access.AspNet;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAccessControl();
builder.Services.AddAccessAspNet();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<
        IAccessSubjectProvider,
        DevelopmentFakeSubjectProvider>();
}

var app = builder.Build();
app.UseRouting();
app.UseAccessControl();

// Starter endpoint only. Add domain APIs together with server-side authorization.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "security-lab-api",
    note = "Initial scaffold; not a production-ready laboratory platform"
}))
.AllowPublic();

// Temporarty protected endpoint
app.MapGet("/api/laboratories/{id}", (string id) =>
{
    return Results.Ok(new
    {
        id,
        message = "Laboratory details"
    });
})
.RequireAccess(
    "lab.read",
    resourceType: "laboratory",
    idRouteParam: "id");

//Temporary
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/access/demo-deny", async (
        IAccessDecision accessDecision,
        CancellationToken cancellationToken) =>
    {
        var decision = await accessDecision.DecideAsync(
            new AccessRequest(
                new SubjectRef("demo-student", AccountState.Active),
                "demo.unknown",
                null,
                Guid.NewGuid().ToString("N")),
            cancellationToken);

        return decision.Outcome == AccessOutcome.Allow
            ? Results.Ok(new { outcome = "allow", decision.CorrelationId })
            : Results.Json(
                new { outcome = "deny", decision.CorrelationId },
                statusCode: StatusCodes.Status403Forbidden);
    })
    .AllowPublic();
}

app.Run();

public partial class Program { }
