using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DynamicCube.Platform.Integration;

/// <summary>
/// Minimal paths into provider JSON: <c>data.result.Licenses[0].Commercial_NO</c>, <c>Licenses[*].Commercial_NO</c>
/// (all elements). A property read on an array reads its first element, so <c>statusDetails.code</c> works whether the
/// provider sends <c>statusDetails</c> as an object (NIC) or as a one-element array (MCI).
/// </summary>
public static partial class JsonPaths
{
    [GeneratedRegex(@"^[A-Za-z_$][\w$-]*(\[(\d+|\*)\])*(\.[A-Za-z_$][\w$-]*(\[(\d+|\*)\])*)*$")]
    private static partial Regex ReadPattern();

    [GeneratedRegex(@"^[A-Za-z_$][\w$-]*(\.[A-Za-z_$][\w$-]*)*$")]
    private static partial Regex WritePattern();

    [GeneratedRegex(@"([^.\[\]]+)|\[(\d+|\*)\]")]
    private static partial Regex Token();

    public static bool IsValid(string? path) => path is not null && ReadPattern().IsMatch(path);

    public static bool IsValidTarget(string? path) => path is not null && WritePattern().IsMatch(path);

    /// <summary>The value at <paramref name="path"/> (a copy), an array when the path contains <c>[*]</c>, or null.</summary>
    public static JsonNode? Read(JsonNode? root, string? path)
    {
        if (root is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(path))
        {
            return root.DeepClone();
        }

        var nodes = new List<JsonNode?> { root };
        var expanded = false;
        foreach (Match token in Token().Matches(path))
        {
            var next = new List<JsonNode?>();
            if (token.Groups[1].Success)
            {
                var name = token.Groups[1].Value;
                foreach (var node in nodes)
                {
                    var target = node is JsonArray array ? (array.Count > 0 ? array[0] : null) : node;
                    if (target is JsonObject obj)
                    {
                        next.Add(Property(obj, name));
                    }
                    else if (expanded)
                    {
                        next.Add(null);
                    }
                }
            }
            else if (token.Groups[2].Value == "*")
            {
                expanded = true;
                foreach (var node in nodes)
                {
                    if (node is JsonArray array)
                    {
                        next.AddRange(array);
                    }
                }
            }
            else
            {
                var index = int.Parse(token.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                foreach (var node in nodes)
                {
                    next.Add(node is JsonArray array && index < array.Count ? array[index] : null);
                }
            }

            nodes = next;
        }

        if (expanded)
        {
            return new JsonArray([.. nodes.Select(n => n?.DeepClone())]);
        }

        return nodes.Count > 0 ? nodes[0]?.DeepClone() : null;
    }

    /// <summary>The value at <paramref name="path"/> as text (numbers and booleans in invariant form), or null.</summary>
    public static string? ReadText(JsonNode? root, string? path) =>
        string.IsNullOrEmpty(path) ? null : Text(Read(root, path));

    public static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonValue value => value.ToJsonString(),
        _ => node.ToJsonString(),
    };

    /// <summary>Sets <paramref name="value"/> at a dot path, creating objects on the way.</summary>
    public static void Write(JsonObject root, string path, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(root);
        var names = path.Split('.');
        var current = root;
        foreach (var name in names[..^1])
        {
            if (current[name] is not JsonObject child)
            {
                child = [];
                current[name] = child;
            }

            current = child;
        }

        current[names[^1]] = value;
    }

    private static JsonNode? Property(JsonObject obj, string name)
    {
        if (obj.TryGetPropertyValue(name, out var value))
        {
            return value;
        }

        foreach (var (key, candidate) in obj)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}
