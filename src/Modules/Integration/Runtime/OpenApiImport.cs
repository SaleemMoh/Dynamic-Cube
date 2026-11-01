using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Platform.Integration;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using DomainLocation = DynamicCube.Modules.Integration.Domain.ParameterLocation;
using OpenApiLocation = Microsoft.OpenApi.ParameterLocation;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>What an API description holds, as the start of a provider, a connection and its operations (ADR-0041).</summary>
public sealed record OpenApiReadResult(
    string Title,
    string? Version,
    string? Description,
    string SuggestedProviderCode,
    IReadOnlyList<string> Servers,
    IReadOnlyList<OpenApiAuthSuggestion> Authentication,
    IReadOnlyList<ImportedOperation> Operations,
    IReadOnlyList<string> Warnings);

/// <summary>A security scheme of the description and the connection settings it suggests (no credentials: those go to the secret store).</summary>
public sealed record OpenApiAuthSuggestion(string Name, AuthType AuthType, string? TokenUrl, string? ApiKeyHeader, string? Note);

/// <summary>An operation of the description: a ready operation definition plus the response fields it may read.</summary>
public sealed record ImportedOperation(
    string Code,
    string Method,
    string PathTemplate,
    string Name,
    string? Description,
    string? Tag,
    IReadOnlyList<OperationParameter> Parameters,
    IReadOnlyList<ImportedField> Fields,
    IReadOnlyList<string> Warnings);

/// <summary>A field of the success response: the output it becomes (name and path) and its type.</summary>
public sealed record ImportedField(string Name, string Path, string Type);

/// <summary>Reads OpenAPI 3.x and Swagger 2.0 descriptions (JSON or YAML) with their references resolved.</summary>
public static partial class OpenApiImport
{
    /// <summary>Largest description read, in characters.</summary>
    public const int MaxLength = 5_000_000;

    private const int MaxBodyDepth = 4;
    private const int MaxOutputDepth = 6;
    private const int MaxFields = 80;
    private static readonly string[] HandledHeaders = ["authorization", "content-type", "accept", "user-agent"];

    public static OpenApiReadResult Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaxLength)
        {
            throw new OpenApiImportException($"The description is larger than {MaxLength / 1_000_000} MB.");
        }

        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();
        ReadResult read;
        try
        {
            read = OpenApiDocument.Parse(text, null, settings);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new OpenApiImportException("The text is not an OpenAPI or Swagger description: " + ex.Message);
        }

        var document = read.Document;
        if (document?.Paths is null || document.Paths.Count == 0)
        {
            var reason = read.Diagnostic?.Errors.FirstOrDefault()?.Message;
            throw new OpenApiImportException("No operations were found in the description" + (reason is null ? "." : ": " + reason));
        }

        var warnings = (read.Diagnostic?.Errors ?? []).Select(e => e.Message).Take(10).ToList();
        var servers = (document.Servers ?? []).Select(s => s.Url?.TrimEnd('/') ?? string.Empty).Where(u => u.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (servers.Count > 0 && servers.All(s => !Uri.TryCreate(s, UriKind.Absolute, out _)))
        {
            warnings.Add("The description's server addresses are relative; type the connection's base URL.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        var operations = new List<ImportedOperation>();
        foreach (var (path, item) in document.Paths)
        {
            foreach (var (method, operation) in item.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                var verb = method.Method.ToUpperInvariant();
                if (verb is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE"))
                {
                    continue;
                }

                operations.Add(Operation(path, verb, item, operation, codes));
            }
        }

        var title = string.IsNullOrWhiteSpace(document.Info?.Title) ? "API" : document.Info.Title.Trim();
        return new OpenApiReadResult(
            title,
            document.Info?.Version,
            document.Info?.Description,
            Truncate(UpperSnake(title), 30),
            servers,
            Authentication(document),
            operations,
            warnings);
    }

    private static ImportedOperation Operation(string path, string method, IOpenApiPathItem item, OpenApiOperation operation, HashSet<string> codes)
    {
        var warnings = new List<string>();
        var template = path;
        var parameters = new List<OperationParameter>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var all = (item.Parameters ?? []).Where(p => operation.Parameters?.Any(o => o.Name == p.Name && o.In == p.In) != true).Concat(operation.Parameters ?? []);
        foreach (var parameter in all)
        {
            if (parameter.Name is null || parameter.In is null)
            {
                continue;
            }

            var location = parameter.In switch
            {
                OpenApiLocation.Path => DomainLocation.Path,
                OpenApiLocation.Query => DomainLocation.Query,
                OpenApiLocation.Header => DomainLocation.Header,
                _ => (DomainLocation?)null,
            };
            if (location is null)
            {
                warnings.Add($"Cookie parameter '{parameter.Name}' is not supported.");
                continue;
            }

            if (location == DomainLocation.Header && HandledHeaders.Contains(parameter.Name.ToLowerInvariant()))
            {
                continue;
            }

            var name = Unique(ParameterName(parameter.Name), names);
            if (location == DomainLocation.Path && name != parameter.Name)
            {
                template = template.Replace("{" + parameter.Name + "}", "{" + name + "}", StringComparison.Ordinal);
            }

            parameters.Add(new OperationParameter
            {
                Name = name,
                Location = location.Value,
                Target = location == DomainLocation.Path || name == parameter.Name ? null : parameter.Name,
                IsRequired = parameter.Required || location == DomainLocation.Path,
                IsMasked = Sensitive(parameter.Name),
                SampleValue = Sample(parameter.Example) ?? Sample(parameter.Schema),
            });
        }

        // Placeholders the description did not declare still need their path parameters.
        foreach (Match placeholder in Placeholder().Matches(template))
        {
            var name = placeholder.Groups[1].Value;
            if (!parameters.Any(p => p.Location == DomainLocation.Path && p.Name == name))
            {
                var fixedName = Unique(ParameterName(name), names);
                template = template.Replace("{" + name + "}", "{" + fixedName + "}", StringComparison.Ordinal);
                parameters.Add(new OperationParameter { Name = fixedName, Location = DomainLocation.Path, IsRequired = true });
            }
        }

        if (method is not "GET" && Json(operation.RequestBody?.Content) is { Schema: { } body })
        {
            var fields = new List<(string Path, bool Required, IOpenApiSchema Schema)>();
            BodyFields(body, string.Empty, true, 0, fields, warnings);
            foreach (var (fieldPath, required, schema) in fields)
            {
                var name = Unique(ParameterName(fieldPath.Split('.')[^1]), names);
                parameters.Add(new OperationParameter
                {
                    Name = name,
                    Location = DomainLocation.Body,
                    Target = fieldPath,
                    IsRequired = required,
                    IsMasked = Sensitive(fieldPath),
                    SampleValue = Sample(schema),
                });
            }
        }

        var outputs = new List<ImportedField>();
        var success = operation.Responses?.Where(r => r.Key.StartsWith('2')).OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Value).FirstOrDefault();
        if (Json(success?.Content) is { Schema: { } responseSchema })
        {
            var outputNames = new HashSet<string>(StringComparer.Ordinal);
            ResponseFields(responseSchema, string.Empty, 0, outputs, outputNames);
            if (outputs.Count >= MaxFields)
            {
                warnings.Add($"Only the first {MaxFields} response fields are offered.");
            }
        }

        var source = operation.OperationId ?? $"{method}_{string.Join('_', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(s => !s.StartsWith('{')))}";
        var code = Unique(Truncate(UpperSnake(source), 40), codes);
        var title = FirstLine(operation.Summary) ?? FirstLine(operation.Description) ?? operation.OperationId ?? $"{method} {path}";
        return new ImportedOperation(code, method, template, Truncate(title, 200), operation.Description, operation.Tags?.FirstOrDefault()?.Name, parameters, outputs, warnings);
    }

    private static void BodyFields(IOpenApiSchema schema, string prefix, bool required, int depth, List<(string, bool, IOpenApiSchema)> fields, List<string> warnings)
    {
        var properties = Properties(schema);
        if (properties.Count == 0)
        {
            if (prefix.Length > 0)
            {
                fields.Add((prefix, required, schema));
            }
            else if (Is(schema, JsonSchemaType.Array))
            {
                warnings.Add("A request body that is a list is not supported; add its fields by hand.");
            }

            return;
        }

        var requiredNames = Required(schema);
        foreach (var (name, property) in properties)
        {
            var path = prefix.Length == 0 ? name : prefix + "." + name;
            if (!JsonPaths.IsValidTarget(path))
            {
                warnings.Add($"Body field '{path}' has a name that cannot be written; add it by hand.");
                continue;
            }

            if (Is(property, JsonSchemaType.Array))
            {
                warnings.Add($"Body field '{path}' is a list; lists are not filled from inputs.");
                continue;
            }

            if (depth + 1 >= MaxBodyDepth && Properties(property).Count > 0)
            {
                warnings.Add($"Body field '{path}' is nested too deeply; add its fields by hand.");
                continue;
            }

            BodyFields(property, path, required && requiredNames.Contains(name), depth + 1, fields, warnings);
        }
    }

    private static void ResponseFields(IOpenApiSchema schema, string prefix, int depth, List<ImportedField> fields, HashSet<string> names)
    {
        if (fields.Count >= MaxFields || depth > MaxOutputDepth)
        {
            return;
        }

        if (Is(schema, JsonSchemaType.Array))
        {
            var listPath = prefix.Length == 0 ? null : prefix + "[*]";
            if (schema.Items is { } items && listPath is not null)
            {
                if (Properties(items).Count == 0)
                {
                    fields.Add(new ImportedField(Unique(OutputName(listPath), names), listPath, TypeOf(items) + "[]"));
                }
                else
                {
                    ResponseFields(items, listPath, depth + 1, fields, names);
                }
            }

            return;
        }

        var properties = Properties(schema);
        if (properties.Count == 0)
        {
            if (prefix.Length > 0)
            {
                fields.Add(new ImportedField(Unique(OutputName(prefix), names), prefix, TypeOf(schema)));
            }

            return;
        }

        foreach (var (name, property) in properties)
        {
            var path = prefix.Length == 0 ? name : prefix + "." + name;
            if (!JsonPaths.IsValid(path))
            {
                continue;
            }

            ResponseFields(property, path, depth + 1, fields, names);
        }
    }

    private static List<OpenApiAuthSuggestion> Authentication(OpenApiDocument document)
    {
        var suggestions = new List<OpenApiAuthSuggestion>();
        foreach (var (name, scheme) in document.Components?.SecuritySchemes ?? new Dictionary<string, IOpenApiSecurityScheme>())
        {
            switch (scheme.Type)
            {
                case SecuritySchemeType.OAuth2 when scheme.Flows?.ClientCredentials is { } flow:
                    suggestions.Add(new OpenApiAuthSuggestion(name, AuthType.OAuth2ClientCredentials, flow.TokenUrl?.ToString(), null, null));
                    break;
                case SecuritySchemeType.OAuth2:
                    suggestions.Add(new OpenApiAuthSuggestion(name, AuthType.None, null, null, "Only the client-credentials flow of OAuth2 is supported."));
                    break;
                case SecuritySchemeType.ApiKey when scheme.In == OpenApiLocation.Header:
                    suggestions.Add(new OpenApiAuthSuggestion(name, AuthType.ApiKey, null, scheme.Name, null));
                    break;
                case SecuritySchemeType.ApiKey:
                    suggestions.Add(new OpenApiAuthSuggestion(name, AuthType.None, null, null, "An API key outside a header is not supported."));
                    break;
                case SecuritySchemeType.Http when string.Equals(scheme.Scheme, "basic", StringComparison.OrdinalIgnoreCase):
                    suggestions.Add(new OpenApiAuthSuggestion(name, AuthType.Basic, null, null, null));
                    break;
                default:
                    suggestions.Add(new OpenApiAuthSuggestion(name, AuthType.None, null, null, $"The '{scheme.Type}' scheme is not supported; choose the authentication by hand."));
                    break;
            }
        }

        // Supported schemes first, the preferred one first.
        return [.. suggestions.OrderBy(s => s.AuthType switch
        {
            AuthType.OAuth2ClientCredentials => 0,
            AuthType.ApiKey => 1,
            AuthType.Basic => 2,
            _ => 3,
        })];
    }

    /// <summary>
    /// The JSON content of a body or response; otherwise the first content with a schema (a Swagger 2.0 description without
    /// <c>produces</c> reads as <c>application/octet-stream</c>).
    /// </summary>
    private static OpenApiMediaType? Json(IDictionary<string, OpenApiMediaType>? content) =>
        content is null ? null
            : content.FirstOrDefault(c => c.Key.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) || c.Key.EndsWith("+json", StringComparison.OrdinalIgnoreCase)).Value
              ?? content.FirstOrDefault(c => c.Key == "*/*").Value
              ?? content.Values.FirstOrDefault(m => m.Schema is not null);

    /// <summary>The properties of a schema, those of its <c>allOf</c> parts included.</summary>
    private static List<KeyValuePair<string, IOpenApiSchema>> Properties(IOpenApiSchema schema)
    {
        var result = new List<KeyValuePair<string, IOpenApiSchema>>(schema.Properties ?? new Dictionary<string, IOpenApiSchema>());
        foreach (var part in schema.AllOf ?? [])
        {
            result.AddRange(Properties(part).Where(p => result.All(r => r.Key != p.Key)));
        }

        return result;
    }

    private static HashSet<string> Required(IOpenApiSchema schema)
    {
        var result = new HashSet<string>(schema.Required ?? new HashSet<string>(), StringComparer.Ordinal);
        foreach (var part in schema.AllOf ?? [])
        {
            result.UnionWith(Required(part));
        }

        return result;
    }

    private static bool Is(IOpenApiSchema schema, JsonSchemaType type) => schema.Type is { } t && t.HasFlag(type);

    private static string TypeOf(IOpenApiSchema schema) =>
        Is(schema, JsonSchemaType.Integer) || Is(schema, JsonSchemaType.Number) ? "number"
        : Is(schema, JsonSchemaType.Boolean) ? "boolean"
        : Is(schema, JsonSchemaType.String) ? (schema.Format is { Length: > 0 } format ? "string:" + format : "string")
        : "value";

    private static string? Sample(IOpenApiSchema? schema) =>
        schema is null ? null : Sample(schema.Examples?.FirstOrDefault()) ?? Sample(LegacyExample(schema)) ?? Sample(schema.Default) ?? (schema.Enum?.Count > 0 ? Sample(schema.Enum[0]) : null);

    // OpenAPI 3.0 and Swagger 2.0 descriptions carry a single example.
#pragma warning disable CS0618
    private static JsonNode? LegacyExample(IOpenApiSchema schema) => schema.Example;
#pragma warning restore CS0618

    private static string? Sample(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value => value.TryGetValue<string>(out var text) ? text : value.ToJsonString(),
        _ => null,
    };

    private static bool Sensitive(string name) => SensitiveName().IsMatch(name);

    private static string ParameterName(string name)
    {
        var cleaned = Regex.Replace(name, "[^A-Za-z0-9_-]+", "_").Trim('_');
        if (cleaned.Length == 0 || !char.IsLetter(cleaned[0]))
        {
            cleaned = "p" + cleaned;
        }

        return Truncate(cleaned, 60);
    }

    /// <summary>An output name from a path: its segments in camel case, without the list markers.</summary>
    private static string OutputName(string path)
    {
        var parts = path.Replace("[*]", string.Empty, StringComparison.Ordinal).Split('.', StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();
        foreach (var part in parts.TakeLast(2))
        {
            var cleaned = Regex.Replace(part, "[^A-Za-z0-9]", string.Empty);
            if (cleaned.Length == 0)
            {
                continue;
            }

            builder.Append(builder.Length == 0 ? char.ToLowerInvariant(cleaned[0]) : char.ToUpperInvariant(cleaned[0])).Append(cleaned.AsSpan(1));
        }

        return ParameterName(builder.Length == 0 ? "value" : builder.ToString());
    }

    /// <summary>A configuration code from free text: words in upper case joined by underscores (camel case split).</summary>
    internal static string UpperSnake(string text)
    {
        var spaced = CamelBoundary().Replace(text, "$1_$2");
        var code = Regex.Replace(spaced, "[^A-Za-z0-9]+", "_").Trim('_').ToUpperInvariant();
        if (code.Length == 0)
        {
            return "API";
        }

        return char.IsLetter(code[0]) ? code : "OP_" + code;
    }

    private static string Unique(string value, HashSet<string> taken)
    {
        var candidate = value;
        for (var i = 2; !taken.Add(candidate); i++)
        {
            candidate = Truncate(value, 60 - 3) + "_" + i.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length].TrimEnd('_');

    private static string? FirstLine(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().Split('\n')[0].Trim();

    [GeneratedRegex(@"\{([^}]*)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex("(password|secret|token|national|iqama|identity|idnumber|mobile|phone|email|birth)", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveName();
}

/// <summary>The text could not be read as an API description.</summary>
public sealed class OpenApiImportException : Exception
{
    public OpenApiImportException()
    {
    }

    public OpenApiImportException(string message)
        : base(message)
    {
    }

    public OpenApiImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
