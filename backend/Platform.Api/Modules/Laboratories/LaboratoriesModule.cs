using Platform.Api.Modules.Laboratories.Api;
using Platform.Api.Modules.Laboratories.Application;
using Platform.Api.Modules.Laboratories.Infrastructure;
using Platform.Assignments.Contracts;
using Platform.Prerequisites.Application;
using Platform.Prerequisites.Contracts;
using Platform.Prerequisites.Infrastructure;

namespace Platform.Api.Modules.Laboratories;

public static class LaboratoriesModule
{
   // Registracija servisa modula
    public static IServiceCollection AddLaboratories(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ILaboratoryRepository, InMemoryLaboratoryRepository>();

        if (environment.IsDevelopment())
        {
            // FAKE implementacije - samo u Development rezimu.
            // FakeIdentityContext - IDN modul (tim 18)
            // FakeAccessDecision  - ACC modul (tim 3)
            services.AddScoped<IIdentityContext, FakeIdentityContext>();
            services.AddSingleton<IAccessDecision, FakeAccessDecision>();
        }
        else
        {
            // Niko nije prepoznat, sve je odbijeno.
            services.AddScoped<IIdentityContext, AnonymousIdentityContext>();
            services.AddSingleton<IAccessDecision, DenyAllAccessDecision>();
        }

        services.AddSingleton<IPrerequisiteRepository, InMemoryPrerequisiteRepository>();
        services.AddSingleton<ICompletedLaboratories, StubCompletedLaboratories>();
        services.AddScoped<IPrerequisiteAccessService, PrerequisiteAccessService>();

        services.AddScoped<
            ILaboratoryPrerequisiteAccess,
            LaboratoryPrerequisiteAccessAdapter>();

        return services;
    }

    public static IEndpointRouteBuilder MapLaboratories(this IEndpointRouteBuilder app)
    {
        app.MapLaboratoriesEndpoints();
        return app;
    }
}