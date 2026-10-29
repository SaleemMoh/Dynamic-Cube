using System.Text.Json.Nodes;
using DynamicCube.Platform.Configuration;

namespace DynamicCube.Platform.Lookups;

/// <summary>Data types of class attributes, as Apps Control names them.</summary>
public static class LookupAttributeTypes
{
    public const string Text = "TEXT";
    public const string Number = "NUMBER";
    public const string Date = "DATE";
    public const string Bool = "BOOL";
    public const string DomainRef = "DOMAIN_REF";
    public const string Json = "JSON";
}

/// <summary>A typed attribute of a product class.</summary>
public sealed record CatalogClassField(string Code, string NameAr, string NameEn, string DataType, bool Required = false, string? RefClass = null);

/// <summary>A value a product class starts with; administrators may rename, reorder or deactivate it afterwards.</summary>
public sealed record CatalogValue(string Code, string NameAr, string NameEn, JsonObject? Attributes = null);

/// <summary>
/// A class (Class/Domain lookup) a module owns: SYSTEM-owned, so its code and attributes come with the product. Its
/// values are configuration (added in change sets) or start from <see cref="Values"/>.
/// </summary>
public sealed record CatalogClass(
    string Code,
    string NameAr,
    string NameEn,
    IReadOnlyList<CatalogClassField> Attributes,
    IReadOnlyList<CatalogValue> Values,
    string? DescriptionAr = null,
    string? DescriptionEn = null);

/// <summary>A provider's code for a value (e.g. a relationship type as the commerce registry names it).</summary>
public sealed record LookupExternalCode(string Provider, string Code, string Direction);

/// <summary>A value of a class as other modules read it.</summary>
public sealed record LookupValue(
    string Code,
    string NameAr,
    string NameEn,
    string? DetailsAr,
    string? DetailsEn,
    string? ParentCode,
    int DisplayOrder,
    bool IsActive,
    bool IsDefault,
    JsonObject Attributes,
    IReadOnlyList<LookupExternalCode> ExternalCodes,
    string? EntityCode = null,
    string? BusinessApp = null)
{
    public string? Text(string attribute) => Attributes[attribute] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public bool Flag(string attribute) => Attributes[attribute] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public int? Whole(string attribute) => Attributes[attribute] is JsonValue v && v.TryGetValue<decimal>(out var d) ? (int)d : null;
}

/// <summary>Values of the Class/Domain lookups (implemented by Apps Control).</summary>
public interface ILookupDirectory
{
    /// <summary>The values of a class, in display order (active ones only unless asked).</summary>
    Task<IReadOnlyList<LookupValue>> ValuesAsync(string classCode, CancellationToken cancellationToken, bool includeInactive = false);

    Task<LookupValue?> FindAsync(string classCode, string code, CancellationToken cancellationToken);
}

/// <summary>
/// A module's rules for the values of one of its classes: what a value must satisfy beyond its typed attributes, and
/// what still uses it (so it cannot be deleted).
/// </summary>
public interface IDomainRules
{
    string ClassCode { get; }

    Task ValidateAsync(string code, JsonObject attributes, ValidationErrors errors, CancellationToken cancellationToken);

    /// <summary>A description of what still uses the value (it then cannot be deleted), or null.</summary>
    Task<string?> UsedByAsync(string code, CancellationToken cancellationToken);
}
