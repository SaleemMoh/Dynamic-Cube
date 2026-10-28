using System.Globalization;
using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Parties;

/// <summary>A sanction in force against a party.</summary>
public sealed record SanctionFact(string Type, bool BlocksApplications, DateOnly StartsOn, DateOnly? EndsOn);

/// <summary>A credential (certificate, license) held by a party.</summary>
public sealed record CredentialFact(string Type, string Number, string Status, DateOnly? IssuedOn, DateOnly? ExpiresOn, JsonNode? Data);

/// <summary>
/// What rules may know about a party: kind, names, identifiers by type, profile values by field code, sanctions in force
/// and credentials. Eligibility rules, fee formulas and form prefill read it as <c>party.*</c>.
/// </summary>
public sealed record PartyFacts(
    Guid Id,
    PartyKind Kind,
    string NameAr,
    string NameEn,
    IReadOnlyDictionary<string, string> Identifiers,
    IReadOnlyDictionary<string, JsonNode?> Profile,
    IReadOnlyList<SanctionFact> ActiveSanctions,
    IReadOnlyList<CredentialFact> Credentials)
{
    public bool IsBlocked => ActiveSanctions.Any(s => s.BlocksApplications);

    /// <summary>The facts as the JSON object rules see.</summary>
    public JsonObject ToJson() => new()
    {
        ["id"] = Id.ToString(),
        ["kind"] = Kind == PartyKind.Individual ? "INDIVIDUAL" : "ESTABLISHMENT",
        ["nameAr"] = NameAr,
        ["nameEn"] = NameEn,
        ["identifiers"] = new JsonObject(Identifiers.Select(i => KeyValuePair.Create(i.Key, (JsonNode?)JsonValue.Create(i.Value)))),
        ["profile"] = new JsonObject(Profile.Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone()))),
        ["blocked"] = IsBlocked,
        ["sanctions"] = new JsonArray([.. ActiveSanctions.Select(s => (JsonNode?)new JsonObject
        {
            ["type"] = s.Type,
            ["blocksApplications"] = s.BlocksApplications,
            ["startsOn"] = s.StartsOn.ToString("O", CultureInfo.InvariantCulture),
            ["endsOn"] = s.EndsOn?.ToString("O", CultureInfo.InvariantCulture),
        })]),
        ["credentials"] = new JsonArray([.. Credentials.Select(c => (JsonNode?)new JsonObject
        {
            ["type"] = c.Type,
            ["number"] = c.Number,
            ["status"] = c.Status,
            ["issuedOn"] = c.IssuedOn?.ToString("O", CultureInfo.InvariantCulture),
            ["expiresOn"] = c.ExpiresOn?.ToString("O", CultureInfo.InvariantCulture),
            ["data"] = c.Data?.DeepClone(),
        })]),
    };
}

/// <summary>Reads <see cref="PartyFacts"/> (implemented by the Party Registry).</summary>
public interface IPartyFacts
{
    /// <param name="refreshProfile">Fetch stale integration-sourced profile fields first (failures keep the stored values).</param>
    Task<PartyFacts?> GetAsync(Guid partyId, bool refreshProfile, CancellationToken cancellationToken);
}
