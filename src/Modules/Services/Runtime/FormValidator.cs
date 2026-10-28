using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Lovs;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>A problem with the answers, in both languages; <see cref="Field"/> is a question code (null for whole-form rules).</summary>
public sealed record FieldError(string? Field, string Code, string MessageAr, string MessageEn, string? Step);

public sealed record FormCheck(IReadOnlyList<FieldError> Errors, JsonObject Data)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Validates answers the way the wizard does, so a modified client cannot get around it: section and question visibility
/// and conditional requirements are evaluated on the answers, then types, options, LOV grants, limits, patterns and the
/// version's cross-field rules. Answers to hidden or unknown questions are dropped; read-only prefilled answers are reset
/// to the party's data.
/// </summary>
internal sealed class FormValidator(JsonLogicEvaluator evaluator, ILovValidator lovs)
{
    /// <param name="step">Only this step's questions and rules (moving between steps); null checks everything (submission).</param>
    public async Task<FormCheck> CheckAsync(ResolvedVersion version, JsonObject data, JsonObject context, string? step, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        var uses = VisibleUses(version, data);
        var clean = new JsonObject();
        foreach (var (code, info) in uses)
        {
            var question = version.Questions[code];
            var value = data[code]?.DeepClone();
            if (info.Readonly && question.Prefill is not null)
            {
                value = JsonPaths.Read(context, question.Prefill);
            }

            if (!IsEmpty(value))
            {
                clean[code] = value;
            }

            if (step is not null && info.Step != step)
            {
                continue;
            }

            // A required checkbox must be ticked (e.g. a pledge), as in the wizard.
            if (IsEmpty(value) || (info.Required && question.Type == QuestionType.Checkbox && value!.GetValueKind() == JsonValueKind.False))
            {
                if (info.Required)
                {
                    errors.Add(Error(code, "REQUIRED", "هذا الحقل مطلوب.", "This field is required.", info.Step));
                }

                continue;
            }

            var problem = await CheckValueAsync(question, value!, data, cancellationToken);
            if (problem is not null)
            {
                errors.Add(problem with { Step = info.Step });
            }
        }

        if (errors.Count == 0)
        {
            context["data"] = clean.DeepClone();
            foreach (var rule in version.Definition.Validations.Where(r => step is null || r.Step == step))
            {
                bool passes;
                try
                {
                    passes = evaluator.EvaluateTruthy(rule.Rule, context);
                }
                catch (JsonLogicException)
                {
                    passes = false;
                }

                if (!passes)
                {
                    errors.Add(new FieldError(null, rule.Code, rule.MessageAr, rule.MessageEn, rule.Step));
                }
            }
        }

        return new FormCheck(errors, clean);
    }

    /// <summary>Questions shown for these answers, each once (the first section that shows it decides its step).</summary>
    public Dictionary<string, (string Step, bool Required, bool Readonly)> VisibleUses(ResolvedVersion version, JsonObject data)
    {
        var result = new Dictionary<string, (string Step, bool Required, bool Readonly)>(StringComparer.Ordinal);
        foreach (var step in version.Definition.Steps.Where(s => s.Type == StepType.Form))
        {
            foreach (var section in step.Sections.Where(s => Truthy(s.VisibleWhen, data, true)))
            {
                foreach (var use in section.Questions.Where(q => Truthy(q.VisibleWhen, data, true)))
                {
                    var required = use.Required || Truthy(use.RequiredWhen, data, false);
                    result[use.Code] = result.TryGetValue(use.Code, out var seen)
                        ? (seen.Step, seen.Required || required, seen.Readonly || use.Readonly)
                        : (step.Code, required, use.Readonly);
                }
            }
        }

        return result;
    }

    private bool Truthy(JsonNode? rule, JsonObject data, bool whenMissing)
    {
        if (rule is null)
        {
            return whenMissing;
        }

        try
        {
            return evaluator.EvaluateTruthy(rule, data);
        }
        catch (JsonLogicException)
        {
            return whenMissing;
        }
    }

    private async Task<FieldError?> CheckValueAsync(QuestionSnapshot question, JsonNode value, JsonObject data, CancellationToken cancellationToken)
    {
        var code = question.Code;
        var rules = question.Validation;
        switch (question.Type)
        {
            case QuestionType.Text or QuestionType.Textarea:
                if (value is not JsonValue v || !v.TryGetValue<string>(out var text))
                {
                    return Invalid(code);
                }

                if (rules.MaxLength is { } max && text.Length > max)
                {
                    return Error(code, "MAX_LENGTH", $"الحد الأقصى {max} حرفاً.", $"At most {max} characters.");
                }

                if (rules.Pattern is { } pattern && !Regex.IsMatch(text, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)))
                {
                    return Error(code, "PATTERN", "الصيغة غير صحيحة.", "The format is not valid.");
                }

                return null;
            case QuestionType.Number:
                var number = Number(value);
                if (number is null)
                {
                    return Error(code, "NUMBER", "أدخل رقماً.", "Enter a number.");
                }

                if (number < rules.Min)
                {
                    return Error(code, "MIN", $"أقل قيمة مسموحة {Format(rules.Min!.Value)}.", $"The minimum is {Format(rules.Min!.Value)}.");
                }

                return number > rules.Max ? Error(code, "MAX", $"أعلى قيمة مسموحة {Format(rules.Max!.Value)}.", $"The maximum is {Format(rules.Max!.Value)}.") : null;
            case QuestionType.Date:
                return value is JsonValue dv && dv.TryGetValue<string>(out var date) && DateOnly.TryParseExact(date[..Math.Min(10, date.Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    ? null
                    : Error(code, "DATE", "أدخل تاريخاً صحيحاً.", "Enter a valid date.");
            case QuestionType.Checkbox:
                return value.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? null : Invalid(code);
            case QuestionType.Select:
                var values = question.Multiple
                    ? (value as JsonArray)?.Select(Text).ToList()
                    : [Text(value)];
                if (values is null || values.Any(x => x is null))
                {
                    return Invalid(code);
                }

                foreach (var choice in values)
                {
                    var allowed = question.Lov is null
                        ? question.Options.Any(o => o.Value == choice)
                        : await lovs.IsAllowedAsync(question.Lov, choice!, question.LovParentQuestion is null ? null : Text(data[question.LovParentQuestion]), cancellationToken);
                    if (!allowed)
                    {
                        return Error(code, "OPTION", "القيمة المختارة غير متاحة.", "The selected value is not available.");
                    }
                }

                return null;
            default:
                return Invalid(code);
        }
    }

    private static FieldError Invalid(string code) => Error(code, "INVALID", "القيمة غير صحيحة.", "The value is not valid.");

    private static FieldError Error(string code, string kind, string ar, string en, string? step = null) => new(code, kind, ar, en, step);

    private static bool IsEmpty(JsonNode? value) => value switch
    {
        null => true,
        JsonValue v when v.TryGetValue<string>(out var s) => string.IsNullOrWhiteSpace(s),
        JsonArray a => a.Count == 0,
        _ => false,
    };

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static decimal? Number(JsonNode value)
    {
        if (value is not JsonValue v)
        {
            return null;
        }

        if (v.TryGetValue<decimal>(out var d))
        {
            return d;
        }

        return v.TryGetValue<string>(out var s) && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
