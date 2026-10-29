using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// What eligibility rules, validations and fee formulas see: <c>data</c> (answers by question code), <c>level</c>,
/// <c>today</c>, <c>party</c> (the beneficiary's facts), <c>applicant</c> (relationship of the person filling it in) and, for a
/// follow-up request, <c>document</c> (number, status, dates, data and lines of the document it is made for).
/// Section and question visibility rules see the answers only, exactly like the wizard.
/// </summary>
internal static class RuleContext
{
    public static JsonObject Build(PartyFacts party, ActingParty acting, JsonObject data, string? level, DateTimeOffset now, DocumentFacts? document = null) => new()
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
        ["document"] = document?.ToJson(),
    };
}
