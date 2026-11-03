using Platform.Access.Composition;
using Platform.Access.Contracts;
using Platform.Access.Domain.Enums;
using Platform.Access.Domain.Models;
using Platform.Api.Domain.Identity;
using Platform.Api.Middlewares;
using Platform.Api.Services.Identity;
using Platform.Audit.Composition;
using Platform.LabVisibility.Composition;
using Platform.LabVisibility.Contracts;
using Platform.LabVisibility.Infrastructure.Membership;

// Oba modula imaju interfejs IAccessDecision, pa se koriste aliasi da ime ne bude dvosmisleno.
using IdentityAccessDecision = Platform.Api.Services.Identity.IAccessDecision;
using PlatformAccessDecision = Platform.Access.Contracts.IAccessDecision;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAccessControl();
builder.Services.AddAudit();
builder.Services.AddLabVisibility();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAccountRepository, InMemoryAccountRepository>(); // TODO: zameniti trajnim repozitorijumom (PostgreSQL)
builder.Services.AddSingleton<ICredentialAuthenticator, UnavailableCredentialAuthenticator>(); // zameniti IDN-02 implementacijom
builder.Services.AddSingleton<ISessionIssuer, UnavailableSessionIssuer>(); // zameniti IDN-02 implementacijom
builder.Services.AddSingleton<IAuditWriter, LoggerAuditWriter>();
builder.Services.AddSingleton<IAdminDirectory, ConfigAdminDirectory>();
builder.Services.AddSingleton<IdentityAccessDecision, AdminOnlyAccessDecision>();
builder.Services.AddScoped<ChangeAccountStatus>();
builder.Services.AddScoped<LoginAccount>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IIdentityContext, StubIdentityContext>();
    builder.Services.AddSingleton<ISessionManager, DevAlwaysActiveSessionManager>(); // samo za lokalni razvoj
}
else
{
    builder.Services.AddScoped<IIdentityContext, AnonymousIdentityContext>();
    builder.Services.AddSingleton<ISessionManager, NoOpSessionRevoker>();   // zameniti kad IDN-02 bude gotov
}

var app = builder.Build();

app.Services.RegisterLabVisibilityAccess();

// Probni nalozi samo za lokalni razvoj (izmišljeni domen example.com).
if (app.Environment.IsDevelopment())
{
    var repo = app.Services.GetRequiredService<IAccountRepository>();
    repo.Save(new UserAccount(Guid.Parse("11111111-1111-1111-1111-111111111111"), "admin@example.com"));
    repo.Save(new UserAccount(Guid.Parse("22222222-2222-2222-2222-222222222222"), "student@example.com"));
    repo.Save(new UserAccount(Guid.Parse("33333333-3333-3333-3333-333333333333"), "other@example.com"));

    // AUT-04/AUT-07 demo kurs: 1111 je nastavnik, 2222 student, 3333 nije na kursu.
    var demoCourse = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    var membership = app.Services.GetRequiredService<InMemoryCourseMembership>();
    membership.Set(demoCourse, Guid.Parse("11111111-1111-1111-1111-111111111111"), CourseRole.Teacher);
    membership.Set(demoCourse, Guid.Parse("22222222-2222-2222-2222-222222222222"), CourseRole.Student);
}

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "security-lab-api",
    note = "Initial scaffold; not a production-ready laboratory platform"
}));

app.UseMiddleware<SessionValidationMiddleware>();

app.MapControllers();

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