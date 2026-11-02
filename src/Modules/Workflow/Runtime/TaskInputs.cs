using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Definitions;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>
/// Reads what an employee filled in on a task against the task's fields: unknown fields are dropped, values are brought
/// to their type (text trimmed, numbers and dates parsed, choices checked) and, when the task moves on, required fields
/// must be there.
/// </summary>
internal static class TaskInputs
{
    private const int MaxText = 500;
    private const int MaxLongText = 4000;

    /// <param name="complete">The task is being done with an action that moves on, so required fields must be filled.</param>
    public static JsonObject Clean(IReadOnlyList<TaskField> fields, JsonObject? given, bool complete)
    {
        var clean = new JsonObject();
        foreach (var field in fields)
        {
            var value = Read(field, given?[field.Code]);
            if (value is null)
            {
                if (complete && field.Required)
                {
                    throw new WorkflowValidationException($"inputs.{field.Code}", $"{field.LabelEn}: this field is required.");
                }

                continue;
            }

            if (complete && field.Required && field.Type == TaskFieldType.Checkbox && !value.GetValue<bool>())
            {
                throw new WorkflowValidationException($"inputs.{field.Code}", $"{field.LabelEn}: this must be ticked.");
            }

            clean[field.Code] = value;
        }

        return clean;
    }

    private static JsonValue? Read(TaskField field, JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        var at = $"inputs.{field.Code}";
        switch (field.Type)
        {
            case TaskFieldType.Text:
            case TaskFieldType.LongText:
                {
                    var text = Text(value)?.Trim();
                    if (string.IsNullOrEmpty(text))
                    {
                        return null;
                    }

                    var most = field.Type == TaskFieldType.Text ? MaxText : MaxLongText;
                    if (text.Length > most)
                    {
                        throw new WorkflowValidationException(at, $"{field.LabelEn}: at most {most} characters.");
                    }

                    return JsonValue.Create(text);
                }

            case TaskFieldType.Number:
                {
                    decimal number;
                    if (value is JsonValue v && v.GetValueKind() == JsonValueKind.Number)
                    {
                        // Read from its JSON text: the value may hold an int, a double or a decimal.
                        if (!decimal.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                        {
                            throw new WorkflowValidationException(at, $"{field.LabelEn}: enter a number.");
                        }
                    }
                    else if (Text(value) is { } text && !string.IsNullOrWhiteSpace(text))
                    {
                        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
                        {
                            throw new WorkflowValidationException(at, $"{field.LabelEn}: enter a number.");
                        }
                    }
                    else
                    {
                        return null;
                    }

                    if (number < field.Min || number > field.Max)
                    {
                        throw new WorkflowValidationException(at, $"{field.LabelEn}: must be between {field.Min?.ToString(CultureInfo.InvariantCulture) ?? "…"} and {field.Max?.ToString(CultureInfo.InvariantCulture) ?? "…"}.");
                    }

                    return JsonValue.Create(number);
                }

            case TaskFieldType.Date:
                {
                    var text = Text(value)?.Trim();
                    if (string.IsNullOrEmpty(text))
                    {
                        return null;
                    }

                    // A date picker may send a full timestamp; the day is what counts.
                    if (text.Length > 10)
                    {
                        text = text[..10];
                    }

                    if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        throw new WorkflowValidationException(at, $"{field.LabelEn}: enter a date (yyyy-mm-dd).");
                    }

                    return JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

            case TaskFieldType.Checkbox:
                return value is JsonValue b && b.GetValueKind() is JsonValueKind.True or JsonValueKind.False
                    ? JsonValue.Create(b.GetValue<bool>())
                    : throw new WorkflowValidationException(at, $"{field.LabelEn}: must be yes or no.");

            case TaskFieldType.Select:
                {
                    var text = Text(value);
                    if (string.IsNullOrEmpty(text))
                    {
                        return null;
                    }

                    return field.Options.Any(o => o.Value == text)
                        ? JsonValue.Create(text)
                        : throw new WorkflowValidationException(at, $"{field.LabelEn}: choose one of the choices.");
                }

            default:
                return null;
        }
    }

    private static string? Text(JsonNode value) => value is JsonValue v
        ? v.GetValueKind() switch
        {
            JsonValueKind.String => v.GetValue<string>(),
            JsonValueKind.Number => v.ToJsonString(),
            _ => null,
        }
        : null;
}
