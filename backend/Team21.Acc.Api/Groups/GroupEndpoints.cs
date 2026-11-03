
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Team21.Acc.Api.Extensions;
using Team21.Acc.Core.Groups.Interfaces;

namespace Team21.Acc.Api.Groups;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(
            "/api/courses/{courseId:guid}/groups");

        // Pregled grupa
        group.MapGet("/", GetGroupsAsync);

        // Pregled konkretne grupe
        group.MapGet("/{groupId:guid}", GetGroupAsync);

        // Pregled clanova grupe
        group.MapGet(
            "/{groupId:guid}/members",
            GetMembersAsync);

        // Dodavanje clana u grupu
        // TODO: Enable after ACC-01 course status integration.
        // group.MapPost(
        //     "/{groupId:guid}/members",
        //     AddMemberAsync);

        // Uklanjanje clana iz grupe
        group.MapDelete(
            "/{groupId:guid}/members/{userId:guid}",
            RemoveMemberAsync);

        return app;
    }

    private static async Task<IResult> GetGroupsAsync(
        Guid courseId,
        IGroupService groupService,
        CancellationToken ct)
    {
        var result = await groupService.GetGroupsAsync(
            courseId, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> GetGroupAsync(
        Guid courseId,
        Guid groupId,
        IGroupService groupService,
        CancellationToken ct)
    {
        var result = await groupService.GetGroupAsync(
            courseId, groupId, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> GetMembersAsync(
        Guid courseId,
        Guid groupId,
        IGroupService groupService,
        CancellationToken ct)
    {
        var result = await groupService.GetMembersAsync(
            courseId, groupId, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> AddMemberAsync(
        Guid courseId,
        Guid groupId,
        AddGroupMemberRequest request,
        IGroupService groupService,
        CancellationToken ct)
    {
        var result = await groupService.AddMemberAsync(
            courseId,
            groupId,
            request.UserId,
            ct);

        return result.ToCreatedHttpResult(
            member =>
                $"/api/courses/{courseId}/groups/{groupId}/members/{member.UserId}");
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid courseId,
        Guid groupId,
        Guid userId,
        IGroupService groupService,
        CancellationToken ct)
    {
        var result = await groupService.RemoveMemberAsync(
            courseId,
            groupId,
            userId,
            ct);

        return result.ToHttpResult();
    }
}

public sealed record AddGroupMemberRequest(Guid UserId);
