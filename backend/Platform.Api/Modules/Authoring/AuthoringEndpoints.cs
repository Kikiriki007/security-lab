using Microsoft.AspNetCore.Mvc;
using Platform.Authoring.Application;
using Platform.Authoring.Domain;
using RequestIdentity = Platform.Api.Modules.Laboratories.Application.IIdentityContext;

namespace Platform.Api.Modules.Authoring;

public static class AuthoringEndpoints
{
    public static IEndpointRouteBuilder MapAuthoringEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/authoring/templates/{templateId:guid}/drafts", CreateFromTemplateAsync).WithTags("Authoring");
        return app;
    }

    private static async Task<IResult> CreateFromTemplateAsync(Guid templateId, HttpContext http, [FromServices] DraftService service,
                                                                [FromServices] RequestIdentity identity, [FromServices] ILoggerFactory loggerFactory)
    {
        //Reuse the request identity registered by LaboratoriesModule
        //the actor ID comes from the request identity, never from the body
        if(!identity.IsAuthenticated || !Guid.TryParse(identity.UserId, out Guid actorId) || actorId == Guid.Empty)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthenticated");
        }

        try
        {
            //DraftService checks IAuthoringAccess before reading the template and stores the new draft through IDraftRepository
            LabDraft draft = await service.CreateDraftFromTemplateAsync(actorId, templateId, http.RequestAborted);

            return Results.Json(new
                                {draft.Id, draft.SourceTemplateId, draft.OwnerId, Status = draft.Status.ToString(), draft.Version, Content = new
                                    {
                                        draft.Content.Title, draft.Content.Goal, Type = draft.Content.Type.ToString()    
                                    }
                                },
                                statusCode: StatusCodes.Status201Created);
        }
        catch(DomainRuleException ex) when (ex.Code == AuthoringErrorCodes.Forbidden)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
        }
        catch(DomainRuleException ex) when (ex.Code == AuthoringErrorCodes.NotFound)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Template not found");
        }
        catch(DomainRuleException ex) when (ex.Code == AuthoringErrorCodes.Conflict)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict");
        }
        catch(Exception ex) when (ex is not OperationCanceledException)
        {
            //Keep exception details in server logs and return a generic error
            loggerFactory.CreateLogger("Authoring.Api").LogError(ex, "Template-based creation failed, traceId={TraceId}", http.TraceIdentifier);
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error");
        }
    }
}