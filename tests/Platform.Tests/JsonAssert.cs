using System.Text.Json;
using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Tests;

/// <summary>Structural JSON comparison where numbers compare by value (1 equals 1.0).</summary>
internal static class JsonAssert
{
    public static bool DeepEquals(JsonNode? expected, JsonNode? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }

        return (expected, actual) switch
        {
            (JsonArray e, JsonArray a) => e.Count == a.Count && e.Zip(a).All(pair => DeepEquals(pair.First, pair.Second)),
            (JsonObject e, JsonObject a) => e.Count == a.Count && e.All(p => a.TryGetPropertyValue(p.Key, out var v) && DeepEquals(p.Value, v)),
            (JsonValue e, JsonValue a) => ValueEquals(e, a),
            _ => false,
        };
    }

    private static bool ValueEquals(JsonValue expected, JsonValue actual)
    {
        var kind = expected.GetValueKind();
        if (kind != actual.GetValueKind())
        {
            return false;
        }

        return kind == JsonValueKind.Number
            ? Math.Abs(double.Parse(expected.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture) - double.Parse(actual.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture)) < 1e-9
            : expected.ToJsonString() == actual.ToJsonString();
    }
}
