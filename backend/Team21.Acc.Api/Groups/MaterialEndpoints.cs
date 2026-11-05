using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Team21.Acc.Api.Extensions;
using Team21.Acc.Core.Common;
using Team21.Acc.Core.Groups;
using Team21.Acc.Core.Groups.Interfaces;
using Team21.Acc.Core.Groups.Visibility;
using Team21.Acc.Core.Groups.Visibility.Interfaces;

namespace Team21.Acc.Api.Groups;

/// <summary>
/// ACC-06 material visibility over HTTP. Every denial answers 404, same as a missing material.
/// </summary>
public static class MaterialEndpoints
{
    public static IEndpointRouteBuilder MapMaterialEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/materials/{type}/{id:guid}", GetMaterialAsync);
        app.MapGet("/api/courses/{courseId:guid}/materials", ListCourseMaterialsAsync);
        app.MapPut("/api/courses/{courseId:guid}/materials/{type}/{id:guid}/groups/{groupId:guid}", GrantGroupAccessAsync);

        return app;
    }

    private static async Task<IResult> GetMaterialAsync(
        string type, Guid id, IMaterialVisibilityService visibility, CancellationToken ct)
    {
        // An unknown type goes in as null, the service answers 401 or 404 without a lookup
        var material = ParseType(type) is { } materialType
            ? new MaterialRef(materialType, id)
            : null;

        var result = await visibility.CanAccessAsync(material, MaterialAction.Read, ct);

        return result.IsSuccess && material is not null
            ? Results.Ok(MaterialResponse.From(material))
            : result.ToHttpResult();
    }

    private static async Task<IResult> ListCourseMaterialsAsync(
        Guid courseId, IMaterialVisibilityService visibility, CancellationToken ct)
    {
        var result = await visibility.FilterVisibleAsync(courseId, ct);
        if (result.IsFailure)
            return Result.Failure(result.Error!).ToHttpResult();

        return Results.Ok(result.Value!.Select(MaterialResponse.From).ToList());
    }

    private static async Task<IResult> GrantGroupAccessAsync(
        Guid courseId,
        string type,
        Guid id,
        Guid groupId,
        IGroupService groupService,
        CancellationToken ct)
    {
        if (ParseType(type) is not { } materialType)
            return Result.Failure(Error.Validation("Invalid material type.")).ToHttpResult();

        var result = await groupService.GrantMaterialAccessAsync(
            courseId,
            groupId,
            new MaterialRef(materialType, id),
            ct);

        return result.ToHttpResult();
    }

    // Names only, a number like "1" is not a valid type
    private static MaterialType? ParseType(string type) =>
        !int.TryParse(type, out _)
        && Enum.TryParse<MaterialType>(type, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : null;
}
