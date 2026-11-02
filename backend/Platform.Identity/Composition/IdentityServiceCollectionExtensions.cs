using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Identity.Domain.Models;
using Platform.Identity.Infrastructure.Persistence;

namespace Platform.Identity.Composition;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddDbContext<AccountDbContext>((sp, options) =>
            options.UseNpgsql(
                sp.GetRequiredService<IConfiguration>().GetConnectionString("Identity")));

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
            })
            .AddEntityFrameworkStores<AccountDbContext>();

        return services;
    }
}