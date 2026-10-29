using DynamicCube.Modules.Visits.Changes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Runtime;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Visits;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.Visits.Endpoints;

internal static class VisitEndpoints
{
    /// <summary>Largest result notification read (external systems send a few kilobytes).</summary>
    private const int MaxCallbackBytes = 256 * 1024;

    /// <summary>Largest field report: up to 8 photos of 3 MB, base64, and a signature.</summary>
    public const long MaxReportBytes = 36L * 1024 * 1024;

    /// <summary>Internal system: the visits register (search, details; scheduling, cancelling and results need EDIT).</summary>
    public static void MapInternal(RouteGroupBuilder group)
    {
        var visits = group.MapGroup("/visits").RequirePrivilege(VisitResources.Registry, CatalogActions.View);
        visits.MapGet("/", async (string? search, string? status, string? type, string? inspector, bool? overdue, int? skip, int? take, VisitRegistry registry, CancellationToken ct) =>
            TypedResults.Ok(await registry.SearchAsync(search, Enum.TryParse<VisitStatus>(status?.Replace("_", string.Empty, StringComparison.Ordinal), true, out var s) ? s : null, type, inspector, overdue ?? false, skip ?? 0, take ?? 50, ct)))
            .WithName("SearchVisits");
        visits.MapGet("/{id:guid}", async (Guid id, VisitRegistry registry, CancellationToken ct) => TypedResults.Ok(await registry.GetAsync(id, ct))).WithName("GetVisit");
        visits.MapGet("/{id:guid}/photos/{photoId:guid}", async (Guid id, Guid photoId, VisitRegistry registry, CancellationToken ct) =>
        {
            var (content, contentType, _) = await registry.PhotoAsync(id, photoId, ct);
            return Results.Stream(content, contentType);
        }).WithName("GetVisitPhoto");
        visits.MapGet("/{id:guid}/signature", async (Guid id, VisitRegistry registry, CancellationToken ct) => Results.Stream(await registry.SignatureAsync(id, ct), "image/png")).WithName("GetVisitSignature");

        var changes = group.MapGroup("/visits").RequirePrivilege(VisitResources.Registry, CatalogActions.Edit);
        changes.MapGet("/inspectors", async (string? type, VisitRegistry registry, CancellationToken ct) => TypedResults.Ok(await registry.InspectorsAsync(type, ct))).WithName("ListInspectors");
        changes.MapPost("/{id:guid}/schedule", async (Guid id, ScheduleVisitRequest request, VisitRegistry registry, CancellationToken ct) => TypedResults.Ok(await registry.ScheduleAsync(id, request, ct))).WithName("ScheduleVisit");
        changes.MapPost("/{id:guid}/cancel", async (Guid id, CancelVisitRequest request, VisitRegistry registry, CancellationToken ct) => TypedResults.Ok(await registry.CancelAsync(id, request, ct))).WithName("CancelVisit");
        changes.MapPost("/{id:guid}/outcome", async (Guid id, RecordOutcomeRequest request, VisitRegistry registry, CancellationToken ct) => TypedResults.Ok(await registry.RecordAsync(id, request, ct))).WithName("RecordVisitOutcome");
        changes.MapPost("/{id:guid}/poll", async (Guid id, ExternalVisits external, VisitRegistry registry, CancellationToken ct) =>
        {
            await external.PollOneAsync(id, ct);
            return TypedResults.Ok(await registry.GetAsync(id, ct));
        }).WithName("PollVisit");

        // The field app: the signed-in inspector's own visits.
        var field = group.MapGroup("/field/visits").RequirePrivilege(VisitResources.Field, CatalogActions.View);
        field.MapGet("/", async (FieldVisits fieldVisits, CancellationToken ct) => TypedResults.Ok(await fieldVisits.MineAsync(ct))).WithName("MyFieldVisits");
        field.MapGet("/{id:guid}", async (Guid id, FieldVisits fieldVisits, CancellationToken ct) => TypedResults.Ok(await fieldVisits.GetAsync(id, ct))).WithName("GetFieldVisit");
        field.MapPost("/{id:guid}/start", async (Guid id, FieldVisits fieldVisits, CancellationToken ct) => TypedResults.Ok(await fieldVisits.StartAsync(id, ct))).WithName("StartFieldVisit");
        field.MapPost("/{id:guid}/report", async (Guid id, FieldReport report, FieldVisits fieldVisits, CancellationToken ct) => TypedResults.Ok(await fieldVisits.ReportAsync(id, report, ct)))
            .WithMetadata(new RequestSizeLimit(MaxReportBytes)).WithName("ReportFieldVisit");
        field.MapGet("/{id:guid}/photos/{photoId:guid}", async (Guid id, Guid photoId, FieldVisits fieldVisits, CancellationToken ct) =>
        {
            var (content, contentType, _) = await fieldVisits.PhotoAsync(id, photoId, ct);
            return Results.Stream(content, contentType);
        }).WithName("GetFieldVisitPhoto");
    }

    /// <summary>External host: result notifications from external inspection systems (anonymous; each must be signed).</summary>
    public static void MapCallbacks(RouteGroupBuilder group)
    {
        group.MapPost("/callbacks/{visitType}", async (string visitType, HttpRequest request, ExternalVisits external, CancellationToken ct) =>
        {
            if (request.ContentLength > MaxCallbackBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);
            if (body.Length > MaxCallbackBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            return await external.HandleAsync(visitType, body, request.Headers[ExternalVisits.SignatureHeader].ToString(), ct) switch
            {
                CallbackOutcome.Accepted => Results.Ok(new { received = true }),
                CallbackOutcome.Unauthorized => Results.Unauthorized(),
                CallbackOutcome.NotFound => Results.NotFound(),
                _ => Results.UnprocessableEntity(),
            };
        }).AllowAnonymous().DisableAntiforgery().WithName("VisitCallback");
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (VisitNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (Exception ex) when (ex is VisitConflictException or VisitException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (VisitValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value));
        }
        catch (FileNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "The file is no longer in storage.");
        }
    }
}

/// <summary>Raises the request body limit of one endpoint (field reports carry photos).</summary>
internal sealed class RequestSizeLimit(long bytes) : Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata
{
    public long? MaxRequestBodySize { get; } = bytes;
}
