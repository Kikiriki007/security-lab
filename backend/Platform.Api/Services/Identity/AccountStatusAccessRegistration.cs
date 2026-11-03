using Platform.Access.Infrastructure.Catalog;
using Platform.Access.Infrastructure.Policies;

namespace Platform.Api.Services.Identity;

/// <summary>
/// Registers the IDN-03 action and policy in the central catalog and registry. Same pattern as
/// <c>RegisterIdentityAccess()</c>: called once after the host is built, before the first request.
/// </summary>
public static class AccountStatusAccessRegistration
{
    public static IServiceProvider RegisterAccountStatusAccess(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var admins = services.GetRequiredService<IAdminDirectory>();

        services.GetRequiredService<InMemoryActionCatalog>()
            .Register(AccountStatusChangePolicy.ActionId);
        services.GetRequiredService<InMemoryAccessPolicyRegistry>()
            .Register(AccountStatusChangePolicy.ActionId, new AccountStatusChangePolicy(admins));

        return services;
    }
}
