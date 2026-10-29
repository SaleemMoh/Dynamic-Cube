using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Changes;

/// <summary>
/// The Party Registry's classes in Class/Domain: relationship, credential and sanction types, edited as
/// <c>party.relationship-type</c>, <c>party.credential-type</c> and <c>party.sanction-type</c>.
/// </summary>
public static class PartyClasses
{
    public const string RelationshipType = "PARTY_RELATIONSHIP_TYPE";
    public const string CredentialType = "PARTY_CREDENTIAL_TYPE";
    public const string SanctionType = "PARTY_SANCTION_TYPE";

    public const string CanAct = "CAN_ACT";
    public const string ExternalCodes = "EXTERNAL_CODES";
    public const string PartyKind = "PARTY_KIND";
    public const string HasExpiry = "HAS_EXPIRY";
    public const string BlocksApplications = "BLOCKS_APPLICATIONS";
    public const string DefaultDays = "DEFAULT_DAYS";

    private const string Mci = "MCI";

    internal static IReadOnlyList<CatalogClass> All { get; } =
    [
        new(RelationshipType, "أنواع العلاقات", "Relationship types",
        [
            new(CanAct, "يمنح صفة التمثيل", "Gives an acting context", LookupAttributeTypes.Bool),
            new(ExternalCodes, "رموز المزودين", "Provider codes", LookupAttributeTypes.Json),
        ],
        [
            // MCI relation codes from the MCI integration guide lookups.
            Relationship("OWNER", "مالك", "Owner", true, "1"),
            Relationship("PARTNER", "شريك", "Partner", true, "2", "3", "4", "5"),
            Relationship("MANAGER", "مدير", "Manager", true, "20", "24", "26", "29", "33", "35"),
            Relationship("DELEGATE", "مفوض", "Authorized delegate", true),
            Relationship("BOARD_MEMBER", "عضو مجلس إدارة أو مديرين", "Board member", false, "10", "11", "12", "13", "14", "30", "31", "32", "34", "36", "37"),
            Relationship("OTHER", "صفة أخرى", "Other role", false, "19", "21", "22", "28", "38", "40"),
        ],
        "صفة الفرد لدى المنشأة (مالك، شريك، مدير، مفوض…)؛ ما يمنح صفة التمثيل يتيح التقديم نيابة عنها.",
        "How an individual relates to a party (owner, partner, manager, delegate…); those that give an acting context let them apply for it."),
        new(CredentialType, "أنواع الوثائق", "Credential types",
        [
            new(PartyKind, "نوع المتعامل", "Party kind", LookupAttributeTypes.Text),
            new("ISSUER_AR", "الجهة المصدرة بالعربية", "Issuer (Arabic)", LookupAttributeTypes.Text),
            new("ISSUER_EN", "الجهة المصدرة بالإنجليزية", "Issuer (English)", LookupAttributeTypes.Text),
            new(HasExpiry, "لها تاريخ انتهاء", "Has an expiry date", LookupAttributeTypes.Bool),
        ],
        [],
        "وثائق تصدرها جهات أخرى وتُسجل للمتعامل (شهادات، تراخيص).",
        "Records issued by other authorities and kept for a party (certificates, licenses)."),
        new(SanctionType, "أنواع الجزاءات", "Sanction types",
        [
            new(BlocksApplications, "يمنع التقديم", "Blocks applications", LookupAttributeTypes.Bool),
            new(DefaultDays, "المدة الافتراضية (أيام)", "Default duration (days)", LookupAttributeTypes.Number),
        ],
        [new("APPLICATION_BAN", "منع من التقديم", "Ban from applying", new JsonObject { [BlocksApplications] = true, [DefaultDays] = 180 })],
        "جزاءات تُسجل على المتعامل وتتحقق منها قواعد الأهلية.",
        "Bans and penalties recorded against a party and checked by eligibility rules."),
    ];

    /// <summary>The provider codes of a relationship type (<c>[{provider, code}]</c>).</summary>
    public static IEnumerable<(string Provider, string Code)> ProviderCodes(LookupValue type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Codes(type.Attributes);
    }

    internal static IEnumerable<(string Provider, string Code)> Codes(JsonObject attributes) =>
        (attributes[ExternalCodes] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(c => (Text(c, "provider") ?? string.Empty, Text(c, "code") ?? string.Empty));

    /// <summary>A missing flag takes its default (credentials expire and sanctions block applications unless said otherwise).</summary>
    public static bool Flag(LookupValue value, string attribute, bool fallback)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Attributes[attribute] is null ? fallback : value.Flag(attribute);
    }

    internal static string? TextOf(JsonObject attributes, string attribute) =>
        attributes[attribute] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    private static string? Text(JsonObject item, string name) =>
        item.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;

    private static CatalogValue Relationship(string code, string nameAr, string nameEn, bool canAct, params string[] codes) =>
        new(code, nameAr, nameEn, new JsonObject
        {
            [CanAct] = canAct,
            [ExternalCodes] = new JsonArray([.. codes.Select(c => (JsonNode)new JsonObject { ["provider"] = Mci, ["code"] = c })]),
        });
}

/// <summary>Provider codes are complete and translate to one relationship type only; a type in use cannot be deleted.</summary>
internal sealed class RelationshipTypeRules(PartiesDbContext context, ILookupDirectory lookups) : IDomainRules
{
    public string ClassCode => PartyClasses.RelationshipType;

    public async Task ValidateAsync(string code, JsonObject attributes, ValidationErrors errors, CancellationToken cancellationToken)
    {
        if (attributes[PartyClasses.ExternalCodes] is { } node && node is not JsonArray)
        {
            errors.Add(PartyClasses.ExternalCodes, "Give a list of {provider, code}.");
            return;
        }

        var codes = PartyClasses.Codes(attributes).ToList();
        if (codes.Any(c => c.Provider.Length == 0 || c.Code.Length == 0))
        {
            errors.Add(PartyClasses.ExternalCodes, "Each provider code needs a provider and a code.");
        }

        if (codes.Distinct().Count() != codes.Count)
        {
            errors.Add(PartyClasses.ExternalCodes, "A provider code is listed twice.");
        }

        // A provider code may point to one relationship type only, or translation would be ambiguous.
        var others = (await lookups.ValuesAsync(ClassCode, cancellationToken, includeInactive: true)).Where(t => t.Code != code).ToList();
        foreach (var (provider, value) in codes.Distinct())
        {
            var clash = others.FirstOrDefault(t => PartyClasses.ProviderCodes(t).Contains((provider, value)));
            if (clash is not null)
            {
                errors.Add(PartyClasses.ExternalCodes, $"{provider} code '{value}' is already mapped to '{clash.Code}'.");
            }
        }

        attributes[PartyClasses.CanAct] ??= false;
    }

    public async Task<string?> UsedByAsync(string code, CancellationToken cancellationToken) =>
        await context.Relationships.AnyAsync(r => r.RelationshipCode == code, cancellationToken) ? "relationships; deactivate it instead" : null;
}

/// <summary>A credential type is for individuals or establishments; one in use cannot be deleted.</summary>
internal sealed class CredentialTypeRules(PartiesDbContext context) : IDomainRules
{
    private static readonly string[] Kinds = [.. Enum.GetNames<PartyKind>().Select(k => k.ToUpperInvariant())];

    public string ClassCode => PartyClasses.CredentialType;

    public Task ValidateAsync(string code, JsonObject attributes, ValidationErrors errors, CancellationToken cancellationToken)
    {
        var kind = PartyClasses.TextOf(attributes, PartyClasses.PartyKind)?.ToUpperInvariant() ?? Kinds[0];
        if (!Kinds.Contains(kind))
        {
            errors.Add(PartyClasses.PartyKind, $"Use one of {string.Join(", ", Kinds)}.");
        }

        errors.MaxLength("ISSUER_AR", PartyClasses.TextOf(attributes, "ISSUER_AR"), 500);
        errors.MaxLength("ISSUER_EN", PartyClasses.TextOf(attributes, "ISSUER_EN"), 500);
        attributes[PartyClasses.PartyKind] = kind;
        attributes[PartyClasses.HasExpiry] ??= true;
        return Task.CompletedTask;
    }

    public async Task<string?> UsedByAsync(string code, CancellationToken cancellationToken) =>
        await context.Credentials.AnyAsync(c => c.CredentialTypeCode == code, cancellationToken) ? "credentials; deactivate it instead" : null;
}

/// <summary>A sanction's default duration is 1 to 3650 days; a type in use cannot be deleted.</summary>
internal sealed class SanctionTypeRules(PartiesDbContext context) : IDomainRules
{
    public string ClassCode => PartyClasses.SanctionType;

    public Task ValidateAsync(string code, JsonObject attributes, ValidationErrors errors, CancellationToken cancellationToken)
    {
        if (attributes[PartyClasses.DefaultDays] is JsonValue days && (!days.TryGetValue<decimal>(out var value) || value is < 1 or > 3650 || value != Math.Floor(value)))
        {
            errors.Add(PartyClasses.DefaultDays, "Must be a whole number between 1 and 3650.");
        }

        attributes[PartyClasses.BlocksApplications] ??= true;
        return Task.CompletedTask;
    }

    public async Task<string?> UsedByAsync(string code, CancellationToken cancellationToken) =>
        await context.Sanctions.AnyAsync(s => s.SanctionTypeCode == code, cancellationToken) ? "sanctions; deactivate it instead" : null;
}
