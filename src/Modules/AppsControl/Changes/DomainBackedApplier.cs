using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Lookups;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Lookups;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>
/// A configuration type whose rows are values of a class (Class/Domain): edited under its own change type and screen,
/// stored as domain values with their fields as typed attributes. Field <c>descriptionAr</c> is attribute
/// <c>DESCRIPTION_AR</c>.
/// </summary>
/// <param name="EntityType">The change type, e.g. <c>svc.category</c>.</param>
/// <param name="ResourceCode">The resource whose privileges guard it (the module's screen).</param>
internal sealed record DomainBackedType(string EntityType, string ClassCode, string ResourceCode);

/// <summary>The types kept in Class/Domain; their classes and attributes come from the owning modules' catalogs.</summary>
internal static class DomainBackedTypes
{
    public static IReadOnlyList<DomainBackedType> All { get; } =
    [
        new("svc.category", "SVC_CATEGORY", "services.config"),
        new("party.relationship-type", "PARTY_RELATIONSHIP_TYPE", "party.config"),
        new("party.credential-type", "PARTY_CREDENTIAL_TYPE", "party.config"),
        new("party.sanction-type", "PARTY_SANCTION_TYPE", "party.config"),
    ];
}

/// <summary>A row of a <see cref="DomainBackedType"/>: the common fields, the display order and the type's own fields.</summary>
public sealed class DomainBackedPayload : ConfigPayload
{
    public int DisplayOrder { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Fields { get; set; }
}

internal sealed class DomainBackedApplier(AppsControlDbContext db, DomainBackedType type, IEnumerable<IDomainRules> rules)
    : ConfigApplier<DomainValue, DomainBackedPayload>(db)
{
    public override string EntityType => type.EntityType;

    public override string Area => AppsControlAreas.Lookups;

    public override string ResourceCode => type.ResourceCode;

    protected override IQueryable<DomainValue> Query(AppsControlDbContext context) =>
        context.Domains.Include(d => d.Class).Where(d => d.Class!.Code == type.ClassCode);

    protected override IQueryable<DomainValue> ListQuery(AppsControlDbContext context, string? parent) =>
        Query(context).OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code);

    protected override async Task ApplyAsync(DomainValue entity, DomainBackedPayload payload, RefResolver refs, ValidationErrors errors, DomainValue? existing, CancellationToken cancellationToken)
    {
        var cls = await refs.Context.Classes.AsNoTracking().Include(c => c.Attributes).SingleOrDefaultAsync(c => c.Code == type.ClassCode, cancellationToken);
        if (cls is null)
        {
            errors.Add("code", $"Class {type.ClassCode} is missing; run the migrator.");
            return;
        }

        var attributes = new JsonObject();
        foreach (var (name, value) in payload.Fields ?? [])
        {
            if (value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                attributes[AttributeCode(name)] = JsonNode.Parse(value.GetRawText());
            }
        }

        var problems = new ValidationErrors();
        await AttributeValues.ValidateAsync(cls.Attributes, attributes, refs, problems, string.Empty, cancellationToken);
        foreach (var rule in rules.Where(r => r.ClassCode == type.ClassCode))
        {
            await rule.ValidateAsync(entity.Code, attributes, problems, cancellationToken);
        }

        problems.CopyTo(errors, FieldName);
        entity.ClassId = cls.Id;
        entity.DisplayOrder = payload.DisplayOrder;
        entity.Attributes = attributes.ToJsonString();
    }

    protected override Task<DomainBackedPayload> ToPayloadAsync(AppsControlDbContext context, DomainValue entity, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (code, value) in JsonNode.Parse(entity.Attributes) as JsonObject ?? [])
        {
            fields[FieldName(code)] = JsonSerializer.SerializeToElement(value);
        }

        return Task.FromResult(new DomainBackedPayload { DisplayOrder = entity.DisplayOrder, Fields = fields });
    }

    protected override async Task BeforeDeleteAsync(AppsControlDbContext context, DomainValue entity, CancellationToken cancellationToken)
    {
        foreach (var rule in rules.Where(r => r.ClassCode == type.ClassCode))
        {
            if (await rule.UsedByAsync(entity.Code, cancellationToken) is { } usage)
            {
                throw new ChangeSetException($"'{entity.Code}' is used by {usage}.");
            }
        }
    }

    /// <summary><c>descriptionAr</c> → <c>DESCRIPTION_AR</c>.</summary>
    public static string AttributeCode(string field)
    {
        var builder = new StringBuilder(field.Length + 4);
        foreach (var c in field)
        {
            if (char.IsUpper(c) && builder.Length > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary><c>DESCRIPTION_AR</c> → <c>descriptionAr</c>; anything else stays as it is.</summary>
    public static string FieldName(string attribute)
    {
        if (attribute.Length == 0 || attribute.Any(char.IsLower))
        {
            return attribute;
        }

        var parts = attribute.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select((p, i) => i == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
