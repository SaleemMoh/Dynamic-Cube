using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Visits.Domain;

namespace DynamicCube.Modules.Visits.Runtime;

/// <summary>Checks a visit result against its type: a known outcome and the form fields, typed and required as configured.</summary>
internal static class VisitForms
{
    /// <summary>The cleaned values (only the type's fields, in their types); throws with every problem found.</summary>
    public static JsonObject Check(VisitType type, string? outcome, JsonObject? data, Dictionary<string, List<string>>? problems = null)
    {
        var errors = problems ?? [];
        if (string.IsNullOrWhiteSpace(outcome))
        {
            Add(errors, "outcome", "Choose the outcome.");
        }
        else if (type.Outcomes.All(o => o.Code != outcome))
        {
            Add(errors, "outcome", $"'{outcome}' is not an outcome of this visit type.");
        }

        var values = data ?? [];
        var clean = new JsonObject();
        foreach (var field in type.Fields)
        {
            var key = $"data.{field.Code}";
            var value = values[field.Code];
            var text = value is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;
            var empty = value is null || (text is not null && text.Length == 0);
            if (empty)
            {
                if (field.Required && field.Type != VisitFieldType.Checkbox)
                {
                    Add(errors, key, "Required.");
                }

                if (field.Type == VisitFieldType.Checkbox)
                {
                    clean[field.Code] = false;
                }

                continue;
            }

            switch (field.Type)
            {
                case VisitFieldType.Number:
                    if (value is JsonValue && decimal.TryParse(text ?? value.ToJsonString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    {
                        clean[field.Code] = number;
                    }
                    else
                    {
                        Add(errors, key, "Must be a number.");
                    }

                    break;
                case VisitFieldType.Date:
                    if (text is not null && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                    {
                        clean[field.Code] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        Add(errors, key, "Must be a date (yyyy-MM-dd).");
                    }

                    break;
                case VisitFieldType.Select:
                    if (text is not null && field.Options.Any(o => o.Code == text))
                    {
                        clean[field.Code] = text;
                    }
                    else
                    {
                        Add(errors, key, "Choose one of the listed values.");
                    }

                    break;
                case VisitFieldType.Checkbox:
                    if (value is JsonValue b && b.TryGetValue<bool>(out var flag))
                    {
                        clean[field.Code] = flag;
                    }
                    else
                    {
                        Add(errors, key, "Must be true or false.");
                    }

                    if (field.Required && clean[field.Code]?.GetValue<bool>() != true)
                    {
                        Add(errors, key, "Must be ticked.");
                    }

                    break;
                default:
                    if (text is null)
                    {
                        Add(errors, key, "Must be text.");
                    }
                    else if (text.Length > (field.Type == VisitFieldType.Textarea ? 4000 : 500))
                    {
                        Add(errors, key, "Too long.");
                    }
                    else
                    {
                        clean[field.Code] = text;
                    }

                    break;
            }
        }

        if (problems is null && errors.Count > 0)
        {
            throw new VisitValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }

        return clean;
    }

    public static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list))
        {
            errors[field] = list = [];
        }

        list.Add(message);
    }
}

public sealed class VisitValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The visit result is not valid: " + string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}")))
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public VisitValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }
}

public sealed class VisitNotFoundException(string message) : Exception(message);

public sealed class VisitConflictException(string message) : Exception(message);
