using Platform.Authoring.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Authoring.Infrastructure.Persistence;

namespace Platform.Authoring.Composition;

public static class AuthoringServiceCollectionExtensions
{
    public static IServiceCollection AddAuthoringModule(
        this IServiceCollection services)
    {
        services.AddDbContext<AuthoringDbContext>(
            (serviceProvider, options) =>
            {
                var configuration = serviceProvider
                    .GetRequiredService<IConfiguration>();

                var connectionString = configuration
                    .GetConnectionString("Authoring");

                options.UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "authoring"));
            });

        services.AddScoped<ILaboratoryRepository, LaboratoryRepository>();

        return services;
    }
}
