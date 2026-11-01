using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Team21.Acc.Core.Groups;
using Team21.Acc.Infrastructure.Groups.Services;

namespace Team21.Acc.Api.Groups;

/// <summary>
/// ACC-06 services and endpoints are registered here.
/// </summary>
public static class GroupsModule
{
    public static IServiceCollection AddGroups(this IServiceCollection services)
    {
        // services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IGroupMembershipReader, GroupMembershipReader>();
        services.AddScoped<IMaterialAccessRuleReader, MaterialAccessRuleReader>();
        return services;
    }

    public static IEndpointRouteBuilder MapGroups(this IEndpointRouteBuilder app)
    {
        // app.MapGroupEndpoints();      // routes under /api/courses/{courseId}/groups
        // app.MapMaterialEndpoints();   // routes under /api/materials
        return app;
    }
}
