using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Modules.Documents.Runtime;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.Documents.Endpoints;

internal static class DocumentEndpoints
{
    /// <summary>Internal system: the registry, status actions, PDFs and the template preview.</summary>
    public static void MapInternal(RouteGroupBuilder group)
    {
        var registry = group.MapGroup("/documents").RequirePrivilege(DocumentResources.Registry, CatalogActions.View);
        registry.MapGet("/", async (string? search, string? type, string? status, Guid? holder, string? app, int? skip, int? take, DocumentRegistry service, CancellationToken ct) =>
            TypedResults.Ok(await service.SearchAsync(search, type, ParseStatus(status), holder, skip ?? 0, take ?? 50, ct, app))).WithName("SearchDocuments");
        registry.MapGet("/by-source/{sourceType}/{sourceId:guid}", async (string sourceType, Guid sourceId, DocumentRegistry service, CancellationToken ct) =>
            TypedResults.Ok(await service.BySourceAsync(sourceType, sourceId, ct))).WithName("GetDocumentsBySource");
        registry.MapGet("/{id:guid}", async (Guid id, DocumentRegistry service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, null, ct))).WithName("GetDocument");
        registry.MapGet("/{id:guid}/versions/{version:int}/pdf", async (Guid id, int version, DocumentRegistry service, CancellationToken ct) =>
            Pdf(await service.PdfAsync(id, version, null, ct))).WithName("GetDocumentPdf");
        registry.MapGet("/{id:guid}/versions/{version:int}/html", async (Guid id, int version, DocumentRegistry service, CancellationToken ct) =>
            Results.Content(await service.HtmlAsync(id, version, ct), "text/html; charset=utf-8")).WithName("GetDocumentHtml");

        var changes = group.MapGroup("/documents/{id:guid}").RequirePrivilege(DocumentResources.Registry, CatalogActions.Edit);
        changes.MapPost("/suspend", async (Guid id, StatusRequest request, DocumentRegistry service, CancellationToken ct) => await Then(service.SuspendAsync(id, request.Comment, ct), service, id, ct)).WithName("SuspendDocument");
        changes.MapPost("/reinstate", async (Guid id, StatusRequest request, DocumentRegistry service, CancellationToken ct) => await Then(service.ReinstateAsync(id, request.Comment, ct), service, id, ct)).WithName("ReinstateDocument");
        changes.MapPost("/cancel", async (Guid id, StatusRequest request, DocumentRegistry service, CancellationToken ct) => await Then(service.CancelAsync(id, request.Comment, ct), service, id, ct)).WithName("CancelDocument");

        group.MapPost("/templates/preview", async (TemplatePreviewRequest request, DocumentRegistry service, CancellationToken ct) =>
        {
            var (content, type) = await service.PreviewAsync(request, ct);
            return Results.File(content, type);
        }).RequirePrivilege(DocumentResources.Config, CatalogActions.View).WithName("PreviewDocumentTemplate");
    }

    /// <summary>Portal: the acting party's documents and their PDFs.</summary>
    public static void MapExternal(RouteGroupBuilder group)
    {
        group.MapGet("/my", async (IActingPartyAccessor acting, DocumentRegistry service, CancellationToken ct) =>
            TypedResults.Ok(await service.SearchAsync(null, null, null, (await acting.GetAsync(ct)).PartyId, 0, 200, ct))).WithName("ListMyDocuments");
        group.MapGet("/my/by-source/{sourceType}/{sourceId:guid}", async (string sourceType, Guid sourceId, IActingPartyAccessor acting, DocumentRegistry service, CancellationToken ct) =>
        {
            var party = (await acting.GetAsync(ct)).PartyId;
            return TypedResults.Ok((await service.BySourceAsync(sourceType, sourceId, ct)).Where(d => d.HolderPartyId == party).ToList());
        }).WithName("GetMyDocumentsBySource");
        group.MapGet("/my/{id:guid}", async (Guid id, IActingPartyAccessor acting, DocumentRegistry service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, (await acting.GetAsync(ct)).PartyId, ct))).WithName("GetMyDocument");
        group.MapGet("/my/{id:guid}/pdf", async (Guid id, IActingPartyAccessor acting, DocumentRegistry service, CancellationToken ct) =>
            Pdf(await service.PdfAsync(id, null, (await acting.GetAsync(ct)).PartyId, ct))).WithName("GetMyDocumentPdf");
    }

    /// <summary>Anyone holding a document's QR link (no sign-in).</summary>
    public static void MapPublic(RouteGroupBuilder group) =>
        group.MapGet("/verify/{code}", async (string code, DocumentRegistry service, CancellationToken ct) => TypedResults.Ok(await service.VerifyAsync(code.Trim().ToUpperInvariant(), ct)))
            .AllowAnonymous().WithName("VerifyDocument");

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (DocumentValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
        catch (DocumentNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (Exception ex) when (ex is DocumentConflictException or DocumentIssueException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (TemplateException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["html"] = [ex.Message] });
        }
        catch (PdfRenderException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: ex.Message);
        }
        catch (ActingPartyException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
    }

    private static IResult Pdf(DocumentFile file) => Results.File(file.Content, "application/pdf", file.FileName);

    private static async Task<IResult> Then(Task change, DocumentRegistry service, Guid id, CancellationToken ct)
    {
        await change;
        return TypedResults.Ok(await service.GetAsync(id, null, ct));
    }

    private static DocumentStatus? ParseStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : Enum.TryParse<DocumentStatus>(value, ignoreCase: true, out var status) ? status
        : throw new DocumentValidationException("status", $"Unknown status '{value}'.");
}
