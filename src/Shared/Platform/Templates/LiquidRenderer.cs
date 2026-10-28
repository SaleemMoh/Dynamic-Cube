using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using Fluid;

namespace DynamicCube.Platform.Templates;

/// <summary>
/// Renders Liquid templates (documents, messages) over JSON models. Values are HTML-encoded for HTML output and left
/// as they are for plain text (SMS, subjects). Parsed templates are cached by their source.
/// </summary>
public sealed class LiquidRenderer
{
    private const int CacheLimit = 500;
    private static readonly FluidParser Parser = new();

    // Escapes markup characters only, so Arabic stays readable in the HTML.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);
    private readonly ConcurrentDictionary<string, IFluidTemplate> _cache = new(StringComparer.Ordinal);
    private readonly TemplateOptions _options;

    public LiquidRenderer()
    {
        _options = new TemplateOptions { CultureInfo = CultureInfo.InvariantCulture, MaxSteps = 100_000 };
        _options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();
    }

    /// <summary>Whether the source parses; the parser's message otherwise.</summary>
    public static bool TryParse(string? source, out string? error)
    {
        if (Parser.TryParse(source ?? string.Empty, out _, out var message))
        {
            error = null;
            return true;
        }

        error = message;
        return false;
    }

    public string RenderHtml(string source, JsonObject model) => Render(source, model, Html);

    public string RenderText(string source, JsonObject model) => Render(source, model, NullEncoder.Default);

    private string Render(string source, JsonObject model, TextEncoder encoder)
    {
        var template = Parse(source);
        var context = new TemplateContext(_options);
        foreach (var (name, value) in model)
        {
            context.SetValue(name, ToObject(value));
        }

        return template.Render(context, encoder);
    }

    private IFluidTemplate Parse(string source)
    {
        if (_cache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        if (!Parser.TryParse(source, out var template, out var error))
        {
            throw new TemplateException(error);
        }

        if (_cache.Count >= CacheLimit)
        {
            _cache.Clear();
        }

        _cache[source] = template;
        return template;
    }

    /// <summary>JSON as plain .NET values (dictionaries, lists, strings, numbers, booleans) that Liquid can walk.</summary>
    private static object? ToObject(JsonNode? node) => node switch
    {
        null => null,
        JsonObject obj => obj.ToDictionary(p => p.Key, p => ToObject(p.Value), StringComparer.Ordinal),
        JsonArray array => array.Select(ToObject).ToList(),
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Number => decimal.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d
                : double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        },
        _ => null,
    };
}

/// <summary>A template that does not parse or fails to render.</summary>
public sealed class TemplateException(string message) : Exception(message);
