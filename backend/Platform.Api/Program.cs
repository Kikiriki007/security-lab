using Platform.Authoring.Composition;
using Platform.Api.Modules.Laboratories;

using Platform.Access.Composition;
using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;

using Platform.Identity.Composition;
using Platform.Api.Endpoints;
using Platform.Api.Security;

using Platform.Api.Domain.Identity;
using Platform.Api.Middlewares;
using Platform.Api.Modules.Identity.RateLimiting;
using Platform.Api.Services.Identity;
using Platform.Audit.Composition;

// Oba modula imaju IAccessDecision, zato koristimo alias.
using IdentityAccessDecision = Platform.Api.Services.Identity.IAccessDecision;
using PlatformAccessDecision = Platform.Access.Contracts.IAccessDecision;

var builder = WebApplication.CreateBuilder(args);

// Access modul
builder.Services.AddAccessControl();

// IDN-01: registracija naloga
builder.Services.AddIdentityModule();

// Tim 20: perzistencija laboratorija
builder.Services.AddAuthoringModule();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IRequestSubjectResolver, DevelopmentSubjectResolver>();
}
else
{
    builder.Services.AddSingleton<IRequestSubjectResolver, UnauthenticatedSubjectResolver>();
}

// Servisi sa main grane
builder.Services.AddAudit();
builder.Services.AddIdentityRateLimiting(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
builder.Services.AddSingleton<ICredentialAuthenticator, UnavailableCredentialAuthenticator>();
builder.Services.AddSingleton<ISessionIssuer, UnavailableSessionIssuer>();
builder.Services.AddSingleton<IAuditWriter, LoggerAuditWriter>();
builder.Services.AddSingleton<IAdminDirectory, ConfigAdminDirectory>();
builder.Services.AddSingleton<IdentityAccessDecision, AdminOnlyAccessDecision>();

builder.Services.AddScoped<ChangeAccountStatus>();
builder.Services.AddScoped<LoginAccount>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IIdentityContext, StubIdentityContext>();
    builder.Services.AddSingleton<ISessionManager, DevAlwaysActiveSessionManager>();
}
else
{
    builder.Services.AddScoped<IIdentityContext, AnonymousIdentityContext>();
    builder.Services.AddSingleton<ISessionManager, NoOpSessionRevoker>();
}

builder.Services.AddLaboratories(builder.Environment);

var app = builder.Build();
app.UseIdentityRateLimiting();

// IDN-01: politike i endpoint za kreiranje naloga
app.Services.RegisterIdentityAccess();
app.MapAccountEndpoints();

// Probni nalozi samo za lokalni razvoj
if (app.Environment.IsDevelopment())
{
    var repo = app.Services.GetRequiredService<IAccountRepository>();

    repo.Save(new UserAccount(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "admin@example.com"));

    repo.Save(new UserAccount(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "student@example.com"));

    repo.Save(new UserAccount(
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        "other@example.com"));
}

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "security-lab-api",
    note = "Initial scaffold; not a production-ready laboratory platform"
}));

app.UseMiddleware<SessionValidationMiddleware>();

app.MapControllers();
app.MapLaboratories();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/access/demo-deny", async (
        PlatformAccessDecision accessDecision,
        CancellationToken cancellationToken) =>
    {
        var decision = await accessDecision.DecideAsync(
            new AccessRequest(
                new SubjectRef("demo-student", AccountState.Active),
                "demo.unknown",
                null,
                Guid.NewGuid().ToString("N")),
            cancellationToken);

        return Results.Ok(new
        {
            decision.Outcome,
            decision.DenialReason,
            decision.CorrelationId
        });
    });
}

app.Run();

public partial class Program { }
