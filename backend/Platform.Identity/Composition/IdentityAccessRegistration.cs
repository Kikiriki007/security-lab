using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Access.Infrastructure.Catalog;
using Platform.Access.Infrastructure.Policies;
using Platform.Identity.Application;

namespace Platform.Identity.Composition;

public static class IdentityAccessRegistration
{
    public static IServiceProvider RegisterIdentityAccess(this IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();

        services.GetRequiredService<InMemoryActionCatalog>()
            .Register(CreateAccountPolicy.ActionId);
        services.GetRequiredService<InMemoryAccessPolicyRegistry>()
            .Register(CreateAccountPolicy.ActionId, new CreateAccountPolicy(configuration));

        return services;
    }
}