using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Communication.Runtime;

/// <summary>What conditions, recipient rules and templates read: the event's facts plus <c>event</c>, <c>subject</c> and <c>party</c>.</summary>
internal static class Facts
{
    public static JsonObject Of(CommunicationEvent e, PartyFacts? party, DateTimeOffset at)
    {
        var facts = (JsonObject)e.Facts.DeepClone();
        facts["event"] = new JsonObject { ["code"] = e.Code, ["at"] = at.ToString("O", CultureInfo.InvariantCulture) };
        facts["subject"] = new JsonObject { ["type"] = e.SubjectType, ["id"] = e.SubjectId.ToString(), ["number"] = e.SubjectNumber };
        if (party is not null)
        {
            facts["party"] = party.ToJson();
        }
        else if (facts["party"] is null && e.PartyId is { } id)
        {
            facts["party"] = new JsonObject { ["id"] = id.ToString() };
        }

        return facts;
    }

    /// <summary>The facts of a typical event of the type, to try rules and templates against.</summary>
    public static JsonObject Sample(CommunicationEventType type)
    {
        var party = new PartyFacts(
            Guid.Empty, PartyKind.Establishment, "مؤسسة المثال", "Sample Establishment",
            new Dictionary<string, string> { ["CR_NUMBER"] = "1010000000" },
            new Dictionary<string, JsonNode?> { ["EMAIL"] = "info@example.com", ["MOBILE"] = "966500000000" }, [], []);
        return Of(
            new CommunicationEvent(Guid.Empty, type.Code, "SAMPLE", Guid.Empty, "SAMPLE-2026-000001", Guid.Empty, type.Sample),
            party,
            new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(3)));
    }
}
