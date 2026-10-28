using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// What eligibility rules, validations and fee formulas see: <c>data</c> (answers by question code), <c>level</c>,
/// <c>today</c>, <c>party</c> (the beneficiary's facts) and <c>applicant</c> (relationship of the person filling it in).
/// Section and question visibility rules see the answers only, exactly like the wizard.
/// </summary>
internal static class RuleContext
{
    public static JsonObject Build(PartyFacts party, ActingParty acting, JsonObject data, string? level, DateTimeOffset now) => new()
    {
        ["data"] = data.DeepClone(),
        ["level"] = level,
        ["today"] = DateOnly.FromDateTime(now.UtcDateTime).ToString("O", CultureInfo.InvariantCulture),
        ["party"] = party.ToJson(),
        ["applicant"] = new JsonObject
        {
            ["individualId"] = acting.IndividualId.ToString(),
            ["relationship"] = acting.RelationshipCode,
            ["isSelf"] = acting.IsSelf,
        },
    };
}
