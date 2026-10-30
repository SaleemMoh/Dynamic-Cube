using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;

namespace DynamicCube.Modules.AppsControl.Lookups;

/// <summary>Validates domain attribute values (JSON) against the class's typed attribute definitions.</summary>
internal static class AttributeValues
{
    /// <summary>Column names of the generated class views; attribute codes may not use them.</summary>
    public static IReadOnlySet<string> ReservedCodes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "ID", "CODE", "CLASS_CODE", "NAME_AR", "NAME_EN", "DETAILS_AR", "DETAILS_EN", "PARENT_CODE",
        "DISPLAY_ORDER", "IS_DEFAULT", "IS_ACTIVE", "EFFECTIVE_FROM", "EFFECTIVE_TO",
    };

    /// <param name="inList">Whether a value belongs to a list of values (fields of type list of values); not checked when null.</param>
    public static async Task ValidateAsync(
        IReadOnlyCollection<AttributeDefinition> definitions,
        JsonObject values,
        RefResolver refs,
        ValidationErrors errors,
        string fieldPrefix,
        CancellationToken cancellationToken,
        Func<string, string, CancellationToken, Task<bool>>? inList = null)
    {
        var byCode = definitions.ToDictionary(d => d.Code, StringComparer.Ordinal);
        foreach (var (key, _) in values)
        {
            if (!byCode.ContainsKey(key))
            {
                errors.Add(fieldPrefix + key, "Unknown attribute.");
            }
        }

        foreach (var definition in definitions)
        {
            var field = fieldPrefix + definition.Code;
            values.TryGetPropertyValue(definition.Code, out var value);
            if (value is null || (value is JsonValue v && v.GetValueKind() == JsonValueKind.String && string.IsNullOrEmpty(v.GetValue<string>())) || value is JsonArray { Count: 0 })
            {
                if (definition.IsRequired)
                {
                    errors.Add(field, "Required.");
                }

                continue;
            }

            var problem = Check(definition, value);
            if (problem is not null)
            {
                errors.Add(field, problem);
            }
            else if (definition.DataType == AttributeDataType.DomainRef && definition.RefClassCode is not null)
            {
                foreach (var code in Codes(value))
                {
                    await refs.DomainAsync(definition.RefClassCode, code, field, errors, cancellationToken);
                }
            }
            else if (definition.DataType == AttributeDataType.LovRef && definition.RefLovCode is not null && inList is not null)
            {
                foreach (var code in Codes(value))
                {
                    if (!await inList(definition.RefLovCode, code, cancellationToken))
                    {
                        errors.Add(field, $"'{code}' is not a value of list {definition.RefLovCode}.");
                    }
                }
            }
        }
    }

    /// <summary>The code, or the codes of a field taking several.</summary>
    private static IEnumerable<string> Codes(JsonNode value) =>
        value is JsonArray array ? array.Select(i => i!.GetValue<string>()) : [value.GetValue<string>()];

    private static string? Check(AttributeDefinition definition, JsonNode value)
    {
        var rules = string.IsNullOrWhiteSpace(definition.Validation) ? null : JsonNode.Parse(definition.Validation) as JsonObject;
        var kind = value.GetValueKind();
        if (definition.DataType is AttributeDataType.DomainRef or AttributeDataType.LovRef)
        {
            if (definition.IsMultiple)
            {
                return value is JsonArray items && items.All(i => i is JsonValue item && item.GetValueKind() == JsonValueKind.String && item.GetValue<string>().Length > 0)
                    ? (items.Select(i => i!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() == items.Count ? null : "Each value once.")
                    : "Expected a list of codes.";
            }

            return kind == JsonValueKind.String ? null : "Expected a code.";
        }

        switch (definition.DataType)
        {
            case AttributeDataType.Text:
            case AttributeDataType.Textarea:
                if (kind != JsonValueKind.String)
                {
                    return "Expected text.";
                }

                var text = value.GetValue<string>();
                if (rules?["maxLength"] is JsonValue maxLength && text.Length > maxLength.GetValue<int>())
                {
                    return $"At most {maxLength} characters.";
                }

                if (rules?["pattern"] is JsonValue pattern && !Regex.IsMatch(text, pattern.GetValue<string>(), RegexOptions.None, TimeSpan.FromMilliseconds(100)))
                {
                    return "Does not match the required format.";
                }

                return null;
            case AttributeDataType.Number:
                if (kind != JsonValueKind.Number)
                {
                    return "Expected a number.";
                }

                var number = value.GetValue<decimal>();
                if (rules?["min"] is JsonValue min && number < min.GetValue<decimal>())
                {
                    return $"Must be at least {min}.";
                }

                if (rules?["max"] is JsonValue max && number > max.GetValue<decimal>())
                {
                    return $"Must be at most {max}.";
                }

                return null;
            case AttributeDataType.Date:
                return kind == JsonValueKind.String && DateOnly.TryParseExact(value.GetValue<string>(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    ? null
                    : "Expected a date (yyyy-MM-dd).";
            case AttributeDataType.Bool:
                return kind is JsonValueKind.True or JsonValueKind.False ? null : "Expected true or false.";
            default:
                return null;
        }
    }
}
