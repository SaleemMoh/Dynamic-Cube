using System.Text.Json;
using System.Text.Json.Nodes;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>Replaces personal data with <c>***</c> before calls are written to the call log.</summary>
internal static class Masking
{
    public const string Mask = "***";
    private const int MaxLoggedChars = 64 * 1024;

    /// <summary>Masks the values of properties named in <paramref name="fields"/> at any depth; truncates long bodies.</summary>
    public static string? Body(string? body, IReadOnlySet<string> fields)
    {
        if (string.IsNullOrEmpty(body))
        {
            return body;
        }

        string text;
        try
        {
            var node = JsonNode.Parse(body);
            Apply(node, fields);
            text = node?.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? string.Empty;
        }
        catch (JsonException)
        {
            // Not JSON: keep only a short prefix, it cannot be masked reliably.
            text = body.Length > 512 ? body[..512] + "…" : body;
        }

        return text.Length > MaxLoggedChars ? text[..MaxLoggedChars] + "…" : text;
    }

    private static void Apply(JsonNode? node, IReadOnlySet<string> fields)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (fields.Contains(key))
                    {
                        obj[key] = Mask;
                    }
                    else
                    {
                        Apply(obj[key], fields);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Apply(item, fields);
                }

                break;
        }
    }
}
