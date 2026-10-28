using System.Text.Json.Nodes;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>
/// Facts about the signed-in user that grant rules and LOV parameters can use, exposed to JSONLogic as <c>user.*</c>
/// (e.g. <c>{"var":"user.orgUnitAttributes.REGION"}</c>) plus <c>app</c>.
/// </summary>
public sealed record UserContext(
    Guid? UserId,
    string UserName,
    string App,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Positions,
    IReadOnlyList<string> OrgUnits,
    string? PrimaryOrgUnit,
    JsonObject OrgUnitAttributes,
    IReadOnlyList<string> SubtreeOrgUnits)
{
    public static UserContext Anonymous(string app) => new(null, "anonymous", app, [], [], [], null, [], []);

    /// <summary>Representative context used to check rules when they are saved.</summary>
    public static UserContext Sample() =>
        new(Guid.Empty, "sample", "INTERNAL", ["ROLE"], ["POSITION"], ["UNIT"], "UNIT", new JsonObject { ["REGION"] = "R1" }, ["UNIT", "SUB_UNIT"]);

    public JsonObject ToJson() => new()
    {
        ["app"] = App,
        ["user"] = new JsonObject
        {
            ["id"] = UserId?.ToString(),
            ["username"] = UserName,
            ["roles"] = ToArray(Roles),
            ["positions"] = ToArray(Positions),
            ["orgUnits"] = ToArray(OrgUnits),
            ["orgUnit"] = PrimaryOrgUnit,
            ["orgUnitAttributes"] = OrgUnitAttributes.DeepClone(),
            ["subtreeOrgUnits"] = ToArray(SubtreeOrgUnits),
        },
    };

    private static JsonArray ToArray(IEnumerable<string> values) => new(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
}
