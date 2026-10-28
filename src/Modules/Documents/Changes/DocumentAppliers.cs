using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Templates;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Documents.Changes;

internal abstract class DocumentApplierBase<TEntity, TPayload>(DocumentsDbContext context)
    : ConfigApplier<DocumentsDbContext, ConfigRefs<DocumentsDbContext>, TEntity, TPayload>(context)
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public override string Area => DocumentChangeTypes.Area;

    public override string ResourceCode => DocumentResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<DocumentsDbContext> CreateRefs(DocumentsDbContext context, ChangeSet? draft) => new(context, draft);
}

public sealed class DocumentTemplatePayload : ConfigPayload
{
    /// <summary>HTML with Liquid: <c>document</c>, <c>holder</c>, <c>data</c>, <c>lines</c>, <c>qr</c>, <c>verifyUrl</c>.</summary>
    public string? Html { get; set; }

    /// <summary>A4 (default), A5 or LETTER.</summary>
    public string? PageSize { get; set; }

    public bool Landscape { get; set; }
}

internal sealed class DocumentTemplateApplier(DocumentsDbContext db) : DocumentApplierBase<DocumentTemplate, DocumentTemplatePayload>(db)
{
    private static readonly string[] PageSizes = ["A4", "A5", "LETTER"];

    public override string EntityType => DocumentChangeTypes.Template;

    protected override IQueryable<DocumentTemplate> Query(DocumentsDbContext context) => context.Templates;

    protected override Task ApplyAsync(DocumentTemplate entity, DocumentTemplatePayload payload, ConfigRefs<DocumentsDbContext> refs, ValidationErrors errors, DocumentTemplate? existing, CancellationToken cancellationToken)
    {
        errors.Required("html", payload.Html);
        errors.MaxLength("html", payload.Html, 500_000);
        if (!string.IsNullOrWhiteSpace(payload.Html) && !LiquidRenderer.TryParse(payload.Html, out var error))
        {
            errors.Add("html", $"The template does not parse: {error}");
        }

        var size = string.IsNullOrWhiteSpace(payload.PageSize) ? "A4" : payload.PageSize.Trim().ToUpperInvariant();
        if (!PageSizes.Contains(size))
        {
            errors.Add("pageSize", "Use A4, A5 or LETTER.");
        }

        entity.Html = payload.Html ?? string.Empty;
        entity.PageSize = size;
        entity.Landscape = payload.Landscape;
        return Task.CompletedTask;
    }

    protected override Task<DocumentTemplatePayload> ToPayloadAsync(DocumentsDbContext context, DocumentTemplate entity, CancellationToken cancellationToken) =>
        Task.FromResult(new DocumentTemplatePayload { Html = entity.Html, PageSize = entity.PageSize, Landscape = entity.Landscape });

    protected override async Task BeforeDeleteAsync(DocumentsDbContext context, DocumentTemplate entity, CancellationToken cancellationToken)
    {
        if (await context.DocumentTypes.AnyAsync(t => t.TemplateId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Template '{entity.Code}' is used by a document type.");
        }
    }
}

public sealed class DocumentFieldPayload
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public bool IsPublic { get; set; }
}

public sealed class DocumentTypePayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>Upper-case letters and digits: numbers read <c>PREFIX-YYYY-000001</c>.</summary>
    public string? NumberPrefix { get; set; }

    public int? ValidityMonths { get; set; }

    public bool OnePerHolder { get; set; }

    public bool AmendResetsValidity { get; set; }

    public string? TemplateCode { get; set; }

    public List<int> AlertDaysBefore { get; set; } = [];

    public List<DocumentFieldPayload> Fields { get; set; } = [];

    public List<DocumentFieldPayload> LineFields { get; set; } = [];
}

internal sealed partial class DocumentTypeApplier(DocumentsDbContext db) : DocumentApplierBase<DocumentType, DocumentTypePayload>(db)
{
    public override string EntityType => DocumentChangeTypes.DocumentType;

    protected override IQueryable<DocumentType> Query(DocumentsDbContext context) => context.DocumentTypes.Include(t => t.Template);

    protected override async Task ApplyAsync(DocumentType entity, DocumentTypePayload payload, ConfigRefs<DocumentsDbContext> refs, ValidationErrors errors, DocumentType? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 4000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 4000);
        errors.Required("numberPrefix", payload.NumberPrefix);
        if (!string.IsNullOrWhiteSpace(payload.NumberPrefix) && !Prefix().IsMatch(payload.NumberPrefix))
        {
            errors.Add("numberPrefix", "Use 1 to 10 upper-case letters and digits, starting with a letter.");
        }

        if (payload.ValidityMonths is < 1 or > 600)
        {
            errors.Add("validityMonths", "Validity is 1 to 600 months, or empty for documents that do not expire.");
        }

        if (payload.AlertDaysBefore.Any(d => d is < 1 or > 365) || payload.AlertDaysBefore.Distinct().Count() != payload.AlertDaysBefore.Count)
        {
            errors.Add("alertDaysBefore", "Reminder days are distinct numbers from 1 to 365.");
        }

        if (payload.AlertDaysBefore.Count > 0 && payload.ValidityMonths is null)
        {
            errors.Add("alertDaysBefore", "Reminders need a validity period.");
        }

        CheckFields("fields", payload.Fields, errors);
        CheckFields("lineFields", payload.LineFields, errors);

        entity.DescriptionAr = string.IsNullOrWhiteSpace(payload.DescriptionAr) ? null : payload.DescriptionAr.Trim();
        entity.DescriptionEn = string.IsNullOrWhiteSpace(payload.DescriptionEn) ? null : payload.DescriptionEn.Trim();
        entity.NumberPrefix = payload.NumberPrefix?.Trim() ?? string.Empty;
        entity.ValidityMonths = payload.ValidityMonths;
        entity.OnePerHolder = payload.OnePerHolder;
        entity.AmendResetsValidity = payload.AmendResetsValidity;
        entity.AlertDaysBefore = [.. payload.AlertDaysBefore.OrderByDescending(d => d)];
        entity.Fields = JsonSerializer.Serialize(payload.Fields.Select(Field).ToList(), ChangeJson.Options);
        entity.LineFields = JsonSerializer.Serialize(payload.LineFields.Select(Field).ToList(), ChangeJson.Options);
        errors.Required("templateCode", payload.TemplateCode);
        entity.TemplateId = await refs.ResolveAsync(refs.Context.Templates, DocumentChangeTypes.Template, payload.TemplateCode, "templateCode", errors, cancellationToken);
    }

    protected override Task<DocumentTypePayload> ToPayloadAsync(DocumentsDbContext context, DocumentType entity, CancellationToken cancellationToken) =>
        Task.FromResult(new DocumentTypePayload
        {
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            NumberPrefix = entity.NumberPrefix,
            ValidityMonths = entity.ValidityMonths,
            OnePerHolder = entity.OnePerHolder,
            AmendResetsValidity = entity.AmendResetsValidity,
            TemplateCode = entity.Template?.Code,
            AlertDaysBefore = [.. entity.AlertDaysBefore],
            Fields = Fields(entity.Fields),
            LineFields = Fields(entity.LineFields),
        });

    protected override async Task BeforeDeleteAsync(DocumentsDbContext context, DocumentType entity, CancellationToken cancellationToken)
    {
        if (await context.Documents.AnyAsync(d => d.DocumentTypeId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Documents of type '{entity.Code}' have been issued; deactivate it instead.");
        }
    }

    internal static List<DocumentFieldPayload> Fields(string json) =>
        JsonSerializer.Deserialize<List<DocumentFieldPayload>>(json, ChangeJson.Options) ?? [];

    private static DocumentFieldPayload Field(DocumentFieldPayload f) => new() { Code = f.Code.Trim(), NameAr = f.NameAr.Trim(), NameEn = f.NameEn.Trim(), IsPublic = f.IsPublic };

    private static void CheckFields(string name, List<DocumentFieldPayload> fields, ValidationErrors errors)
    {
        foreach (var (field, i) in fields.Select((f, i) => (f, i)))
        {
            if (!FieldCode().IsMatch(field.Code ?? string.Empty))
            {
                errors.Add($"{name}[{i}].code", "Use letters, digits and _ (a JSON property name).");
            }

            errors.Required($"{name}[{i}].nameAr", field.NameAr);
            errors.Required($"{name}[{i}].nameEn", field.NameEn);
        }

        if (fields.Select(f => f.Code).Distinct(StringComparer.Ordinal).Count() != fields.Count)
        {
            errors.Add(name, "Field codes must be unique.");
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]{0,9}$")]
    private static partial Regex Prefix();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$")]
    private static partial Regex FieldCode();
}
