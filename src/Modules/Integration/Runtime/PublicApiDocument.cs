using System.Text.Json.Nodes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Platform.PublicApi;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>
/// The OpenAPI 3.0 description of the endpoints an API client may call (ADR-0042): paths and parameters, the rows or the
/// function result each returns, the client-credentials sign-in and the problem responses.
/// </summary>
public sealed class PublicApiDocument(IDataQueries queries, IEnumerable<IApiFunction> functions)
{
    public async Task<JsonObject> BuildAsync(string title, IReadOnlyList<ApiEndpoint> endpoints, string? baseUrl, string? tokenUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var paths = new JsonObject();
        foreach (var endpoint in endpoints.OrderBy(e => e.Path, StringComparer.Ordinal))
        {
            paths[endpoint.Path] = new JsonObject { ["get"] = await OperationAsync(endpoint, cancellationToken) };
        }

        return new JsonObject
        {
            ["openapi"] = "3.0.3",
            ["info"] = new JsonObject
            {
                ["title"] = title,
                ["version"] = "1",
                ["description"] = "Read-only endpoints. Get a token with OAuth2 client credentials (your client id and secret) at the token URL, "
                    + "then send it as `Authorization: Bearer <token>`. Lists are paged with `limit` and `offset`.",
            },
            ["servers"] = new JsonArray(new JsonObject { ["url"] = (baseUrl?.TrimEnd('/') ?? string.Empty) + PublicApiSettings.Path }),
            ["security"] = new JsonArray(new JsonObject { ["oauth2"] = new JsonArray() }),
            ["paths"] = paths,
            ["components"] = new JsonObject
            {
                ["securitySchemes"] = new JsonObject
                {
                    ["oauth2"] = new JsonObject
                    {
                        ["type"] = "oauth2",
                        ["flows"] = new JsonObject { ["clientCredentials"] = new JsonObject { ["tokenUrl"] = tokenUrl ?? "/token", ["scopes"] = new JsonObject() } },
                    },
                },
                ["schemas"] = new JsonObject
                {
                    ["Problem"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["title"] = new JsonObject { ["type"] = "string" },
                            ["status"] = new JsonObject { ["type"] = "integer" },
                            ["detail"] = new JsonObject { ["type"] = "string" },
                            ["errors"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["additionalProperties"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
                            },
                        },
                    },
                },
            },
        };
    }

    private async Task<JsonObject> OperationAsync(ApiEndpoint endpoint, CancellationToken cancellationToken)
    {
        var parameters = new JsonArray([.. endpoint.Parameters.Select(p => (JsonNode?)Parameter(p.Name, p.Location == ApiParameterLocation.Path ? "path" : "query", p.IsRequired, Text(p.DescriptionEn, p.DescriptionAr), Type(p.Type), p.Sample))]);
        var list = endpoint.Kind == ApiEndpointKind.Query && !endpoint.SingleRow;
        if (list)
        {
            parameters.Add(Parameter("limit", "query", false, $"Rows per page (1–{endpoint.PageSize}, default {endpoint.PageSize}).", new JsonObject { ["type"] = "integer" }, null));
            parameters.Add(Parameter("offset", "query", false, "Rows to skip (default 0).", new JsonObject { ["type"] = "integer" }, null));
        }

        var responses = new JsonObject { ["200"] = new JsonObject { ["description"] = "OK", ["content"] = Json(await ResultAsync(endpoint, cancellationToken)) } };
        foreach (var (status, text) in Problems(endpoint))
        {
            responses[status] = new JsonObject { ["description"] = text, ["content"] = Json(new JsonObject { ["$ref"] = "#/components/schemas/Problem" }) };
        }

        return new JsonObject
        {
            ["operationId"] = endpoint.Code,
            ["summary"] = endpoint.NameEn,
            ["description"] = Text(endpoint.DescriptionEn, endpoint.DescriptionAr) ?? endpoint.NameAr,
            ["parameters"] = parameters,
            ["responses"] = responses,
        };
    }

    private async Task<JsonObject> ResultAsync(ApiEndpoint endpoint, CancellationToken cancellationToken)
    {
        if (endpoint.Kind == ApiEndpointKind.Function)
        {
            var sample = functions.FirstOrDefault(f => f.Info.Code == endpoint.Function)?.Info.Sample;
            var schema = sample is null ? new JsonObject() : SchemaOf(sample);
            if (sample is not null)
            {
                schema["example"] = sample.DeepClone();
            }

            return schema;
        }

        var fields = endpoint.Fields.Select(f => new DataField(f.Name, f.Column)).ToList();
        var types = await queries.TypesAsync(endpoint.Query is null ? null : JsonNode.Parse(endpoint.Query), fields, [.. endpoint.Parameters.Select(p => p.Name)], cancellationToken);
        var row = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(endpoint.Fields.Select(f =>
            {
                var schema = Column(types.GetValueOrDefault(f.Name));
                if (Text(f.DescriptionEn, f.DescriptionAr) is { } description)
                {
                    schema["description"] = description;
                }

                return KeyValuePair.Create(f.Name, (JsonNode?)schema);
            })),
        };
        return endpoint.SingleRow
            ? row
            : new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["items"] = new JsonObject { ["type"] = "array", ["items"] = row },
                    ["limit"] = new JsonObject { ["type"] = "integer" },
                    ["offset"] = new JsonObject { ["type"] = "integer" },
                    ["hasMore"] = new JsonObject { ["type"] = "boolean" },
                },
            };
    }

    private static IEnumerable<(string Status, string Text)> Problems(ApiEndpoint endpoint)
    {
        if (endpoint.Parameters.Count > 0 || (endpoint.Kind == ApiEndpointKind.Query && !endpoint.SingleRow))
        {
            yield return ("400", "The parameters are invalid.");
        }

        yield return ("401", "No valid token.");
        yield return ("403", "The client may not call this endpoint.");
        if (endpoint.Kind == ApiEndpointKind.Function || endpoint.SingleRow)
        {
            yield return ("404", "Nothing matches the parameters.");
        }

        yield return ("429", "Too many calls: wait for the time in Retry-After.");
    }

    /// <summary>A schema that describes a sample value (objects, lists of the first element, strings, numbers, yes/no).</summary>
    public static JsonObject SchemaOf(JsonNode? sample) => sample switch
    {
        JsonObject o => new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(o.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)SchemaOf(p.Value)))) },
        JsonArray a => new JsonObject { ["type"] = "array", ["items"] = a.Count > 0 ? SchemaOf(a[0]) : new JsonObject() },
        JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.Number => new JsonObject { ["type"] = "number" },
        JsonValue v when v.GetValueKind() is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => new JsonObject { ["type"] = "boolean" },
        JsonValue => new JsonObject { ["type"] = "string" },
        _ => new JsonObject { ["nullable"] = true },
    };

    private static JsonObject Parameter(string name, string location, bool required, string? description, JsonObject schema, string? sample)
    {
        var parameter = new JsonObject { ["name"] = name, ["in"] = location, ["required"] = required, ["schema"] = schema };
        if (description is not null)
        {
            parameter["description"] = description;
        }

        if (!string.IsNullOrWhiteSpace(sample))
        {
            parameter["example"] = sample;
        }

        return parameter;
    }

    private static JsonObject Json(JsonObject schema) => new() { ["application/json"] = new JsonObject { ["schema"] = schema } };

    private static JsonObject Type(ApiParameterType type) => type switch
    {
        ApiParameterType.Number => new JsonObject { ["type"] = "number" },
        ApiParameterType.Date => new JsonObject { ["type"] = "string", ["format"] = "date" },
        ApiParameterType.Boolean => new JsonObject { ["type"] = "boolean" },
        _ => new JsonObject { ["type"] = "string" },
    };

    private static JsonObject Column(string? type) => type switch
    {
        DataTypes.Number => new JsonObject { ["type"] = "number", ["nullable"] = true },
        DataTypes.Date => new JsonObject { ["type"] = "string", ["format"] = "date", ["nullable"] = true },
        DataTypes.Boolean => new JsonObject { ["type"] = "boolean", ["nullable"] = true },
        DataTypes.Text => new JsonObject { ["type"] = "string", ["nullable"] = true },
        _ => new JsonObject { ["nullable"] = true },
    };

    private static string? Text(string? english, string? arabic) =>
        !string.IsNullOrWhiteSpace(english) ? english : string.IsNullOrWhiteSpace(arabic) ? null : arabic;
}
