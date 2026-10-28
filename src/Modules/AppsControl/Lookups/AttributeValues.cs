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

    public static async Task ValidateAsync(
        IReadOnlyCollection<AttributeDefinition> definitions,
        JsonObject values,
        RefResolver refs,
        ValidationErrors errors,
        string fieldPrefix,
        CancellationToken cancellationToken)
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
            if (value is null || (value is JsonValue v && v.GetValueKind() == JsonValueKind.String && string.IsNullOrEmpty(v.GetValue<string>())))
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
                await refs.DomainAsync(definition.RefClassCode, value.GetValue<string>(), field, errors, cancellationToken);
            }
        }
    }

    private static string? Check(AttributeDefinition definition, JsonNode value)
    {
        var rules = string.IsNullOrWhiteSpace(definition.Validation) ? null : JsonNode.Parse(definition.Validation) as JsonObject;
        var kind = value.GetValueKind();
        switch (definition.DataType)
        {
            case AttributeDataType.Text:
            case AttributeDataType.DomainRef:
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
