using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.Workflow.Endpoints;

internal static class WorkflowEndpoints
{
    /// <summary>Action Center: inboxes, tasks, delegations, supervision and instance timelines.</summary>
    public static void Map(RouteGroupBuilder group)
    {
        var center = group.MapGroup(string.Empty).RequirePrivilege(WorkflowResources.ActionCenter, CatalogActions.View);
        center.MapGet("/inbox", async (string? box, string? search, string? app, int? skip, int? take, ActionCenter service, CancellationToken ct) =>
            TypedResults.Ok(await service.InboxAsync(Parse<InboxBox>(box, "box") ?? InboxBox.Mine, search, skip ?? 0, take ?? 50, ct, app))).WithName("GetInbox");
        center.MapGet("/inbox/counts", async (ActionCenter service, CancellationToken ct) => TypedResults.Ok(await service.CountsAsync(ct))).WithName("GetInboxCounts");
        center.MapGet("/tasks/{id:guid}", async (Guid id, ActionCenter service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct))).WithName("GetTask");
        center.MapGet("/tasks/{id:guid}/candidates", async (Guid id, ActionCenter service, CancellationToken ct) => TypedResults.Ok(await service.CandidatesAsync(id, ct))).WithName("GetTaskCandidates");
        center.MapPost("/tasks/{id:guid}/claim", async (Guid id, ActionCenter service, CancellationToken ct) => await Then(service.ClaimAsync(id, ct), service, id, ct)).WithName("ClaimTask");
        center.MapPost("/tasks/{id:guid}/release", async (Guid id, ActionCenter service, CancellationToken ct) => await Then(service.ReleaseAsync(id, ct), service, id, ct)).WithName("ReleaseTask");
        center.MapPost("/tasks/{id:guid}/assign", async (Guid id, AssignRequest request, ActionCenter service, CancellationToken ct) => await Then(service.AssignAsync(id, request, ct), service, id, ct)).WithName("AssignTask");
        center.MapPost("/tasks/{id:guid}/act", async (Guid id, ActRequest request, ActionCenter service, CancellationToken ct) => await Then(service.ActAsync(id, request, ct), service, id, ct)).WithName("ActOnTask");
        center.MapPost("/tasks/{id:guid}/notes", async (Guid id, NoteRequest request, ActionCenter service, CancellationToken ct) => await Then(service.AddNoteAsync(id, request, ct), service, id, ct)).WithName("AddTaskNote");
        center.MapGet("/instances/{subjectType}/{subjectId:guid}", async (string subjectType, Guid subjectId, ActionCenter service, CancellationToken ct) =>
            TypedResults.Ok(await service.InstanceAsync(subjectType, subjectId, ct))).WithName("GetWorkflowInstance");
        center.MapGet("/delegations", async (ActionCenter service, CancellationToken ct) => TypedResults.Ok(await service.DelegationsAsync(ct))).WithName("ListDelegations");
        center.MapPost("/delegations", async (DelegationRequest request, ActionCenter service, CancellationToken ct) => TypedResults.Ok(await service.DelegateAsync(request, ct))).WithName("CreateDelegation");
        center.MapDelete("/delegations/{id:guid}", async (Guid id, ActionCenter service, CancellationToken ct) =>
        {
            await service.CancelDelegationAsync(id, ct);
            return TypedResults.NoContent();
        }).WithName("CancelDelegation");
        center.MapGet("/staff", async (ActionCenter service, CancellationToken ct) => TypedResults.Ok(await service.StaffAsync(ct))).WithName("ListStaff");

        group.MapGet("/supervision", async (string? search, string? role, string? sla, bool? unassigned, int? skip, int? take, ActionCenter service, CancellationToken ct) =>
            TypedResults.Ok(await service.SupervisionAsync(search, role, Parse<SlaState>(sla, "sla"), unassigned, skip ?? 0, take ?? 50, ct)))
            .RequirePrivilege(WorkflowResources.Supervision, CatalogActions.View).WithName("GetSupervision");
    }

    /// <summary>A query value as the API writes enums (MINE, ON_TRACK, …).</summary>
    private static T? Parse<T>(string? value, string field)
        where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null
        : Enum.TryParse<T>(value.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var parsed) ? parsed
        : throw new WorkflowValidationException(field, $"Unknown value '{value}'.");

    /// <summary>After a change, the task as it now is (a closed task may no longer be visible: then nothing).</summary>
    private static async Task<IResult> Then(Task change, ActionCenter service, Guid id, CancellationToken ct)
    {
        await change;
        try
        {
            return TypedResults.Ok(await service.GetAsync(id, ct));
        }
        catch (WorkflowForbiddenException)
        {
            return TypedResults.NoContent();
        }
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (WorkflowValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
        catch (WorkflowNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (WorkflowForbiddenException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
        catch (WorkflowConflictException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }
}
