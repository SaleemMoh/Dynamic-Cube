using DynamicCube.Modules.Communication.Changes;
using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Modules.Communication.Runtime;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.Communication.Endpoints;

internal static class CommunicationEndpoints
{
    /// <summary>Internal system: event types and template preview (configuration), the message log, and the employee's own notifications.</summary>
    public static void MapInternal(RouteGroupBuilder group)
    {
        group.MapGet("/events", (CommunicationService service) => TypedResults.Ok(service.Events()))
            .RequirePrivilege(CommunicationResources.Config, CatalogActions.View).WithName("ListCommunicationEvents");
        group.MapPost("/templates/preview", (TemplatePreviewRequest request, CommunicationService service) => TypedResults.Ok(service.Preview(request)))
            .RequirePrivilege(CommunicationResources.Config, CatalogActions.View).WithName("PreviewMessageTemplate");

        var log = group.MapGroup("/messages").RequirePrivilege(CommunicationResources.Messages, CatalogActions.View);
        log.MapGet("/", async (string? search, string? status, string? channel, string? eventCode, Guid? subjectId, int? skip, int? take, CommunicationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SearchAsync(search, ParseStatus(status), channel, eventCode, subjectId, skip ?? 0, take ?? 50, ct))).WithName("SearchMessages");
        log.MapGet("/{id:guid}", async (Guid id, CommunicationService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct))).WithName("GetMessage");
        group.MapPost("/messages/{id:guid}/resend", async (Guid id, CommunicationService service, CancellationToken ct) => TypedResults.Ok(await service.ResendAsync(id, ct)))
            .RequirePrivilege(CommunicationResources.Messages, CatalogActions.Edit).WithName("ResendMessage");

        MapNotifications(group.MapGroup("/notifications"), (service, _, _) => Task.FromResult(service.Employee()), "Staff");
    }

    /// <summary>Portal: the acting party's notifications.</summary>
    public static void MapExternal(RouteGroupBuilder group) =>
        MapNotifications(group.MapGroup("/my/notifications"), async (_, acting, ct) => new NotificationOwner((await acting!.GetAsync(ct)).PartyId, null), "My");

    private static void MapNotifications(RouteGroupBuilder group, Func<CommunicationService, IActingPartyAccessor?, CancellationToken, Task<NotificationOwner>> owner, string name)
    {
        group.MapGet("/", async (bool? unread, int? take, CommunicationService service, IServiceProvider sp, CancellationToken ct) =>
            TypedResults.Ok(await service.NotificationsAsync(await owner(service, Acting(sp), ct), unread ?? false, take ?? 20, ct))).WithName($"List{name}Notifications");
        group.MapGet("/unread-count", async (CommunicationService service, IServiceProvider sp, CancellationToken ct) =>
            TypedResults.Ok(new { count = await service.UnreadAsync(await owner(service, Acting(sp), ct), ct) })).WithName($"Count{name}UnreadNotifications");
        group.MapPost("/{id:guid}/read", async (Guid id, CommunicationService service, IServiceProvider sp, CancellationToken ct) =>
        {
            await service.ReadAsync(await owner(service, Acting(sp), ct), id, ct);
            return TypedResults.NoContent();
        }).WithName($"Read{name}Notification");
        group.MapPost("/read-all", async (CommunicationService service, IServiceProvider sp, CancellationToken ct) =>
        {
            await service.ReadAsync(await owner(service, Acting(sp), ct), null, ct);
            return TypedResults.NoContent();
        }).WithName($"ReadAll{name}Notifications");
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (CommunicationValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
        catch (CommunicationNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (CommunicationConflictException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (TemplateException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["template"] = [ex.Message] });
        }
        catch (ActingPartyException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
    }

    private static IActingPartyAccessor? Acting(IServiceProvider services) => services.GetService(typeof(IActingPartyAccessor)) as IActingPartyAccessor;

    private static MessageStatus? ParseStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : Enum.TryParse<MessageStatus>(value, ignoreCase: true, out var status) ? status
        : throw new CommunicationValidationException("status", $"Unknown status '{value}'.");
}
