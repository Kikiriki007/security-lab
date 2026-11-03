using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Access.Application.Services;
using Platform.Access.Contracts;
using Platform.Access.Domain.Constants;
using Platform.Access.Infrastructure.Auditing;
using Platform.Access.Infrastructure.Catalog;
using Platform.Access.Infrastructure.Membership;
using Platform.Access.Infrastructure.Policies;
using Platform.Access.Infrastructure.Resources;

namespace Platform.Access.Composition;

public static class AccessServiceCollectionExtensions
{
    public static IServiceCollection AddAccessControl(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryActionCatalog>();
        services.AddSingleton<IActionCatalog>(sp => sp.GetRequiredService<InMemoryActionCatalog>());
        services.TryAddSingleton<IMembershipReader, EmptyMembershipReader>();
        services.TryAddSingleton<IResourceDescriptorResolver, EmptyResourceDescriptorResolver>();
        services.AddSingleton<InMemoryAccessPolicyRegistry>(sp =>
        {
            var registry = new InMemoryAccessPolicyRegistry();
            var membershipReader = sp.GetRequiredService<IMembershipReader>();
            var resourceDescriptorResolver = sp.GetRequiredService<IResourceDescriptorResolver>();

            foreach (var definition in CourseRolePolicies.All)
            {
                registry.Register(
                    definition.Action.Id,
                    new CourseRoleAccessPolicy(
                        definition,
                        membershipReader,
                        resourceDescriptorResolver));
            }

            return registry;
        });
        services.AddSingleton<IAccessPolicyRegistry>(sp => sp.GetRequiredService<InMemoryAccessPolicyRegistry>());
        services.AddSingleton<IAccessAuditWriter, NullAccessAuditWriter>();
        services.AddSingleton<IAccessDecision, DefaultDenyAccessDecision>();

        return services;
    }
}
