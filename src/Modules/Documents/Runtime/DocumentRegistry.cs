using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Platform.Files;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Documents.Runtime;

public sealed record DocumentSummary(
    Guid Id,
    string Number,
    string TypeCode,
    string TypeNameAr,
    string TypeNameEn,
    Guid HolderPartyId,
    string HolderNameAr,
    string HolderNameEn,
    DocumentStatus Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    int CurrentVersion);

public sealed record DocumentPage(IReadOnlyList<DocumentSummary> Items, int Total);

public sealed record FieldView(string Code, string NameAr, string NameEn, bool IsPublic);

public sealed record DocumentVersionView(
    int Version,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    string IssuedBy,
    string SourceType,
    Guid SourceId,
    string? SourceNumber,
    string? Reason,
    JsonObject Data,
    JsonArray Lines,
    bool Rendered,
    string? RenderError);

public sealed record DocumentEventView(DateTimeOffset At, string Type, string Actor, int? Version, string? Comment);

public sealed record DocumentDetail(
    DocumentSummary Document,
    string VerifyUrl,
    IReadOnlyList<FieldView> Fields,
    IReadOnlyList<FieldView> LineFields,
    IReadOnlyList<DocumentVersionView> Versions,
    IReadOnlyList<DocumentEventView> Events);

/// <summary>What anyone with the QR link sees: enough to confirm the document is genuine and in force.</summary>
public sealed record VerifyView(
    string Number,
    string TypeNameAr,
    string TypeNameEn,
    string HolderNameAr,
    string HolderNameEn,
    DocumentStatus Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    int Version,
    DateTimeOffset VersionIssuedAt,
    IReadOnlyList<VerifyField> Fields,
    IReadOnlyList<FieldView> LineFields,
    JsonArray Lines);

public sealed record VerifyField(string NameAr, string NameEn, JsonNode? Value);

public sealed record StatusRequest(string? Comment);

public sealed record TemplatePreviewRequest(string Html, string? PageSize, bool Landscape, JsonObject? Data, JsonArray? Lines, string? Format);

public sealed class DocumentNotFoundException(string message) : Exception(message);

public sealed class DocumentConflictException(string message) : Exception(message);

/// <summary>A PDF as stored or freshly printed.</summary>
public sealed record DocumentFile(Stream Content, string FileName);

/// <summary>
/// The issued-document registry: search, details with versions and history, status changes (suspend, reinstate, cancel),
/// the PDF of a version (printed on first request when the worker has not yet), and public verification.
/// </summary>
internal sealed class DocumentRegistry(
    DocumentsDbContext context,
    DocumentRenderer renderer,
    IFileStorage files,
    ICurrentUser user,
    TimeProvider clock)
{
    /// <param name="app">Only documents of the document types of this business app.</param>
    public async Task<DocumentPage> SearchAsync(string? search, string? type, DocumentStatus? status, Guid? holder, int skip, int take, CancellationToken cancellationToken, string? app = null)
    {
        var query = context.Documents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(app))
        {
            var types = context.DocumentTypes.Where(t => t.BusinessApp == app || t.SharedApps.Contains(app)).Select(t => t.Code);
            query = query.Where(d => types.Contains(d.TypeCode));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(d => EF.Functions.ILike(d.Number, term) || EF.Functions.ILike(d.HolderNameAr, term) || EF.Functions.ILike(d.HolderNameEn, term));
        }

        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(d => d.TypeCode == type);
        if (status is not null) query = query.Where(d => d.Status == status);
        if (holder is not null) query = query.Where(d => d.HolderPartyId == holder);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(d => d.IssuedAt).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        return new DocumentPage(await SummariesAsync(rows, cancellationToken), total);
    }

    /// <summary>Documents issued or amended for a subject (e.g. a service request).</summary>
    public async Task<IReadOnlyList<DocumentSummary>> BySourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken)
    {
        var ids = await context.Versions.Where(v => v.SourceType == sourceType && v.SourceId == sourceId).Select(v => v.DocumentId).Distinct().ToListAsync(cancellationToken);
        var rows = await context.Documents.AsNoTracking().Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken);
        return await SummariesAsync(rows, cancellationToken);
    }

    public async Task<DocumentDetail> GetAsync(Guid id, Guid? holder, CancellationToken cancellationToken)
    {
        var document = await FindAsync(id, holder, cancellationToken);
        var type = await context.DocumentTypes.AsNoTracking().SingleAsync(t => t.Id == document.DocumentTypeId, cancellationToken);
        var versions = await context.Versions.AsNoTracking().Where(v => v.DocumentId == id).OrderByDescending(v => v.Version).ToListAsync(cancellationToken);
        var events = holder is null
            ? await context.Events.AsNoTracking().Where(e => e.DocumentId == id).OrderBy(e => e.At).ThenBy(e => e.Id).ToListAsync(cancellationToken)
            : [];
        return new DocumentDetail(
            (await SummariesAsync([document], cancellationToken))[0],
            renderer.VerifyUrl(document.VerificationCode),
            Fields(type.Fields),
            Fields(type.LineFields),
            [.. versions.Select(v => new DocumentVersionView(
                v.Version, v.IssuedAt, v.ExpiresAt, holder is null ? v.IssuedBy : string.Empty, v.SourceType, v.SourceId, v.SourceNumber, v.Reason,
                JsonNode.Parse(v.Data)!.AsObject(), JsonNode.Parse(v.Lines)!.AsArray(), v.FileKey is not null, holder is null ? v.RenderError : null))],
            [.. events.Select(e => new DocumentEventView(e.At, e.Type, e.Actor, e.Version, e.Comment))]);
    }

    public Task SuspendAsync(Guid id, string? comment, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, comment, [DocumentStatus.Active], DocumentStatus.Suspended, DocumentEventTypes.Suspended, cancellationToken);

    public Task ReinstateAsync(Guid id, string? comment, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, comment, [DocumentStatus.Suspended], DocumentStatus.Active, DocumentEventTypes.Reinstated, cancellationToken);

    public Task CancelAsync(Guid id, string? comment, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, comment, [DocumentStatus.Active, DocumentStatus.Suspended], DocumentStatus.Cancelled, DocumentEventTypes.Cancelled, cancellationToken);

    /// <summary>The PDF of a version (the current one when <paramref name="version"/> is null), printing and storing it if needed.</summary>
    public async Task<DocumentFile> PdfAsync(Guid id, int? version, Guid? holder, CancellationToken cancellationToken)
    {
        var document = await FindAsync(id, holder, cancellationToken);
        var number = version ?? document.CurrentVersion;
        var row = await context.Versions.SingleOrDefaultAsync(v => v.DocumentId == id && v.Version == number, cancellationToken)
            ?? throw new DocumentNotFoundException($"Version {number} of document {document.Number} does not exist.");
        var key = row.FileKey ?? await RenderAsync(row, cancellationToken);
        Stream content;
        try
        {
            content = await files.OpenReadAsync(key, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            // The stored PDF is gone (storage restored from an older backup, or not shared between hosts): print it again.
            content = await files.OpenReadAsync(await RenderAsync(row, cancellationToken), cancellationToken);
        }

        return new DocumentFile(content, $"{document.Number}-v{row.Version}.pdf");
    }

    /// <summary>A version as HTML (the preview shown in the internal system).</summary>
    public async Task<string> HtmlAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var row = await context.Versions.AsNoTracking().SingleOrDefaultAsync(v => v.DocumentId == id && v.Version == version, cancellationToken)
            ?? throw new DocumentNotFoundException("The version does not exist.");
        var (template, model) = await ModelAsync(row, cancellationToken);
        return renderer.Html(template, model, inlineFonts: true);
    }

    /// <summary>Prints a version and stores the PDF; returns the file key. Called by the worker and on first download.</summary>
    public async Task<string> RenderAsync(DocumentVersion version, CancellationToken cancellationToken)
    {
        var (template, model) = await ModelAsync(version, cancellationToken);
        try
        {
            var pdf = await renderer.PdfAsync(template, model, cancellationToken);
            using var stream = new MemoryStream(pdf);
            var stored = await files.SaveAsync(stream, $"{model.Document["number"]}-v{version.Version}.pdf", "application/pdf", cancellationToken);
            version.FileKey = stored.Key;
            version.RenderedAt = clock.GetUtcNow();
            version.RenderError = null;
            await context.SaveChangesAsync(cancellationToken);
            return stored.Key;
        }
        catch (PdfRenderException ex)
        {
            version.RenderError = ex.Message;
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<VerifyView> VerifyAsync(string code, CancellationToken cancellationToken)
    {
        var document = await context.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.VerificationCode == code, cancellationToken)
            ?? throw new DocumentNotFoundException("No document has this verification code.");
        var type = await context.DocumentTypes.AsNoTracking().SingleAsync(t => t.Id == document.DocumentTypeId, cancellationToken);
        var version = await context.Versions.AsNoTracking().SingleAsync(v => v.DocumentId == document.Id && v.Version == document.CurrentVersion, cancellationToken);
        var data = JsonNode.Parse(version.Data)!.AsObject();
        var lineFields = Fields(type.LineFields).Where(f => f.IsPublic).ToList();
        var lines = new JsonArray([.. JsonNode.Parse(version.Lines)!.AsArray().Select(line => (JsonNode?)new JsonObject(
            lineFields.Select(f => KeyValuePair.Create(f.Code, line is JsonObject o ? o[f.Code]?.DeepClone() : null))))]);
        return new VerifyView(
            document.Number, type.NameAr, type.NameEn, document.HolderNameAr, document.HolderNameEn, document.Status, document.IssuedAt, document.ExpiresAt,
            document.CurrentVersion, version.IssuedAt,
            [.. Fields(type.Fields).Where(f => f.IsPublic).Select(f => new VerifyField(f.NameAr, f.NameEn, data[f.Code]?.DeepClone()))],
            lineFields, lines);
    }

    /// <summary>A template rendered with sample data, as HTML or PDF (the template editor's preview).</summary>
    public async Task<(byte[] Content, string ContentType)> PreviewAsync(TemplatePreviewRequest request, CancellationToken cancellationToken)
    {
        var template = new DocumentTemplate { Html = request.Html, PageSize = request.PageSize ?? "A4", Landscape = request.Landscape };
        var model = new DocumentModel(
            new JsonObject
            {
                ["number"] = "SAMPLE-2026-000001",
                ["version"] = 1,
                ["status"] = "ACTIVE",
                ["typeNameAr"] = "مستند تجريبي",
                ["typeNameEn"] = "Sample document",
                ["issuedOn"] = clock.GetUtcNow().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ["expiresOn"] = clock.GetUtcNow().AddYears(2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            },
            new JsonObject { ["nameAr"] = "مؤسسة تجريبية", ["nameEn"] = "Sample establishment", ["identifiers"] = new JsonObject { ["CR_NUMBER"] = "1010000001" } },
            request.Data ?? [],
            request.Lines ?? [],
            renderer.VerifyUrl("SAMPLE"));
        if (string.Equals(request.Format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (await renderer.PdfAsync(template, model, cancellationToken), "application/pdf");
        }

        return (System.Text.Encoding.UTF8.GetBytes(renderer.Html(template, model, inlineFonts: true)), "text/html; charset=utf-8");
    }

    private async Task<(DocumentTemplate Template, DocumentModel Model)> ModelAsync(DocumentVersion version, CancellationToken cancellationToken)
    {
        var document = await context.Documents.AsNoTracking().SingleAsync(d => d.Id == version.DocumentId, cancellationToken);
        var type = await context.DocumentTypes.AsNoTracking().SingleAsync(t => t.Id == document.DocumentTypeId, cancellationToken);
        var template = await context.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == (version.TemplateId ?? type.TemplateId), cancellationToken)
            ?? throw new DocumentConflictException($"Document type '{type.Code}' has no template.");
        var dates = System.Globalization.CultureInfo.InvariantCulture;
        var model = new DocumentModel(
            new JsonObject
            {
                ["number"] = document.Number,
                ["version"] = version.Version,
                ["status"] = document.Status.ToString().ToUpperInvariant(),
                ["typeCode"] = type.Code,
                ["typeNameAr"] = type.NameAr,
                ["typeNameEn"] = type.NameEn,
                ["issuedOn"] = document.IssuedAt.ToString("yyyy-MM-dd", dates),
                ["versionIssuedOn"] = version.IssuedAt.ToString("yyyy-MM-dd", dates),
                ["expiresOn"] = version.ExpiresAt?.ToString("yyyy-MM-dd", dates),
            },
            JsonNode.Parse(version.Holder)!.AsObject(),
            JsonNode.Parse(version.Data)!.AsObject(),
            JsonNode.Parse(version.Lines)!.AsArray(),
            renderer.VerifyUrl(document.VerificationCode));
        return (template, model);
    }

    private async Task ChangeStatusAsync(Guid id, string? comment, DocumentStatus[] from, DocumentStatus to, string eventType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new DocumentValidationException("comment", "Give the reason.");
        }

        var document = await context.Documents.SingleOrDefaultAsync(d => d.Id == id, cancellationToken) ?? throw new DocumentNotFoundException("The document does not exist.");
        if (!from.Contains(document.Status))
        {
            throw new DocumentConflictException($"Document {document.Number} is {document.Status.ToString().ToUpperInvariant()}.");
        }

        var now = clock.GetUtcNow();
        // A suspension may outlast the validity: reinstating an expired document leaves it expired.
        document.Status = to == DocumentStatus.Active && document.ExpiresAt <= now ? DocumentStatus.Expired : to;
        document.StatusChangedAt = now;
        context.Events.Add(new DocumentEvent { DocumentId = id, At = now, Type = eventType, Actor = user.UserName ?? user.UserId, Version = document.CurrentVersion, Comment = comment.Trim() });
        var type = await context.DocumentTypes.AsNoTracking().SingleAsync(t => t.Id == document.DocumentTypeId, cancellationToken);
        var notification = to switch
        {
            DocumentStatus.Suspended => DocumentNotifications.Suspended,
            DocumentStatus.Cancelled => DocumentNotifications.Cancelled,
            _ => DocumentNotifications.Reinstated,
        };
        DocumentNotifications.Raise(context, clock, notification, document, type, renderer.VerifyUrl(document.VerificationCode), comment.Trim());
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DocumentConflictException("Someone else changed the document; reload it.");
        }
    }

    private async Task<IssuedDocument> FindAsync(Guid id, Guid? holder, CancellationToken cancellationToken)
    {
        var document = await context.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        return document is null || (holder is not null && document.HolderPartyId != holder) ? throw new DocumentNotFoundException("The document does not exist.") : document;
    }

    private async Task<List<DocumentSummary>> SummariesAsync(List<IssuedDocument> rows, CancellationToken cancellationToken)
    {
        var typeIds = rows.Select(r => r.DocumentTypeId).Distinct().ToList();
        var types = await context.DocumentTypes.AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
        return [.. rows.Select(d => new DocumentSummary(
            d.Id, d.Number, d.TypeCode, types[d.DocumentTypeId].NameAr, types[d.DocumentTypeId].NameEn, d.HolderPartyId, d.HolderNameAr, d.HolderNameEn,
            d.Status, d.IssuedAt, d.ExpiresAt, d.CurrentVersion))];
    }

    private static List<FieldView> Fields(string json) =>
        [.. DocumentTypeApplier.Fields(json).Select(f => new FieldView(f.Code, f.NameAr, f.NameEn, f.IsPublic))];
}

public sealed class DocumentValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
