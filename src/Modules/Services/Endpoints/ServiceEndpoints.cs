using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.Services.Endpoints;

internal static class ServiceEndpoints
{
    /// <summary>Portal: catalog, service details, drafts and submission for the acting party.</summary>
    public static void MapExternal(RouteGroupBuilder group)
    {
        group.MapGet("/catalog", async (RequestService service, CancellationToken ct) => TypedResults.Ok(await service.CatalogAsync(ct))).WithName("GetServiceCatalog");
        group.MapGet("/services/{code}", async (string code, RequestService service, CancellationToken ct) => TypedResults.Ok(await service.GetServiceAsync(code, ct))).WithName("GetService");
        group.MapGet("/documents/{id:guid}/services", async (Guid id, RequestService service, CancellationToken ct) => TypedResults.Ok(await service.FollowUpsAsync(id, ct))).WithName("ListFollowUpServices");
        group.MapGet("/requests", async (StatusCategory? status, RequestService service, CancellationToken ct) => TypedResults.Ok(await service.ListMineAsync(status, ct))).WithName("ListMyRequests");
        group.MapPost("/requests", async (CreateRequest request, RequestService service, CancellationToken ct) =>
        {
            var view = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/services/v1/requests/{view.Request.Id}", view);
        }).WithName("CreateRequest");
        group.MapGet("/requests/{id:guid}", async (Guid id, RequestService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct))).WithName("GetMyRequest");
        group.MapPut("/requests/{id:guid}", async (Guid id, SaveRequest request, RequestService service, CancellationToken ct) => TypedResults.Ok(await service.SaveAsync(id, request, ct))).WithName("SaveRequest");
        group.MapPost("/requests/{id:guid}/submit", async (Guid id, SubmitRequest request, RequestService service, CancellationToken ct) => TypedResults.Ok(await service.SubmitAsync(id, request, ct))).WithName("SubmitRequest");
        group.MapDelete("/requests/{id:guid}", async (Guid id, RequestService service, CancellationToken ct) =>
        {
            await service.CancelAsync(id, ct);
            return TypedResults.NoContent();
        }).WithName("CancelDraft");
    }

    /// <summary>Portal, before or after sign-in: categories, listed services, service cards and statistics.</summary>
    public static void MapPortal(RouteGroupBuilder group)
    {
        group.MapGet("/portal/catalog", async (PortalCatalogService portal, CancellationToken ct) => TypedResults.Ok(await portal.CatalogAsync(ct)))
            .AllowAnonymous().WithName("GetPortalCatalog");
        group.MapGet("/portal/services/{code}", async (string code, PortalCatalogService portal, CancellationToken ct) => TypedResults.Ok(await portal.CardAsync(code, ct)))
            .AllowAnonymous().WithName("GetServiceCard");
        group.MapGet("/portal/statistics", async (PortalCatalogService portal, CancellationToken ct) => TypedResults.Ok(await portal.StatisticsAsync(ct)))
            .AllowAnonymous().WithName("GetPortalStatistics");
    }

    /// <summary>Internal system: requests (read) and version previews.</summary>
    public static void MapInternal(RouteGroupBuilder group)
    {
        group.MapGet("/admin/requests", async (string? service, StatusCategory? status, string? search, string? app, int? skip, int? take, RequestAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.SearchAsync(service, status, search, skip ?? 0, take ?? 50, ct, app)))
            .RequirePrivilege(ServiceResources.Requests, CatalogActions.View).WithName("SearchRequests");
        group.MapGet("/admin/requests/{id:guid}", async (Guid id, RequestAdminService admin, CancellationToken ct) => TypedResults.Ok(await admin.GetAsync(id, ct)))
            .RequirePrivilege(ServiceResources.Requests, CatalogActions.View).WithName("GetRequest");
        group.MapGet("/admin/versions/{service}/{number:int}", async (string service, int number, RequestAdminService admin, CancellationToken ct) => TypedResults.Ok(await admin.VersionAsync(service, number, ct)))
            .RequirePrivilege(ServiceResources.Config, CatalogActions.View).WithName("GetResolvedVersion");
        group.MapPost("/admin/versions/preview", async (PreviewRequest request, RequestAdminService admin, CancellationToken ct) => TypedResults.Ok(await admin.PreviewAsync(request, ct)))
            .RequirePrivilege(ServiceResources.Config, CatalogActions.View).WithName("PreviewVersion");
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (RequestValidationException ex)
        {
            return Results.Json(new { title = "The request cannot be submitted as it is.", status = 400, errors = ex.Errors }, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (NotEligibleException ex)
        {
            return Results.Json(new { title = "Not eligible.", status = 422, reasons = ex.Reasons }, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (ChangeValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value), title: "The definition is invalid.");
        }
        catch (RequestNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (RequestConflictException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (ActingPartyException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
    }
}
