using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Decisions.Runtime;

public enum CalculatorInputType
{
    Number,
    Text,
    Select,
    Checkbox,
    Date,
}

/// <summary>One field of a public calculator: what the rule set reads as <c>input.&lt;code&gt;</c>.</summary>
public sealed class CalculatorInput
{
    public string Code { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public string? HintAr { get; set; }

    public string? HintEn { get; set; }

    public CalculatorInputType Type { get; set; }

    public bool Required { get; set; }

    public List<CalculatorOption> Options { get; set; } = [];

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    /// <summary>
    /// A JSONLogic rule over <c>{ party }</c> (the signed-in applicant's acting party, e.g. <c>party.profile.CR_CAPITAL</c>)
    /// giving the starting value, so the calculator starts from the firm's latest data.
    /// </summary>
    public JsonNode? Prefill { get; set; }
}

public sealed class CalculatorOption
{
    public string Value { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;
}

public enum CalculatorResultFormat
{
    Text,
    Number,
    Money,
    YesNo,
}

/// <summary>A result the calculator shows: a path in the rule set's output and how to show it.</summary>
public sealed class CalculatorResult
{
    public string Path { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public CalculatorResultFormat Format { get; set; }

    /// <summary>Shown large, as the answer.</summary>
    public bool Headline { get; set; }
}

/// <summary>How a public rule set is shown as a calculator on the portal.</summary>
public sealed class CalculatorDefinition
{
    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string? IntroAr { get; set; }

    public string? IntroEn { get; set; }

    public string? Icon { get; set; }

    public List<CalculatorInput> Inputs { get; set; } = [];

    public List<CalculatorResult> Results { get; set; } = [];

    /// <summary>Shown under the results, e.g. "an estimate, not a decision".</summary>
    public string? NoteAr { get; set; }

    public string? NoteEn { get; set; }

    internal static CalculatorDefinition? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<CalculatorDefinition>(json, ChangeJson.Options);

    internal string ToJson() => JsonSerializer.Serialize(this, ChangeJson.Options);
}

/// <summary>A public calculator as listed on the portal.</summary>
public sealed record CalculatorSummary(string Code, string TitleAr, string TitleEn, string? IntroAr, string? IntroEn, string? Icon);

/// <summary>A public calculator's form and results (prefill rules are not sent, only whether a field has one).</summary>
public sealed record CalculatorView(
    string Code,
    string TitleAr,
    string TitleEn,
    string? IntroAr,
    string? IntroEn,
    string? Icon,
    IReadOnlyList<CalculatorInputView> Inputs,
    IReadOnlyList<CalculatorResult> Results,
    string? NoteAr,
    string? NoteEn);

public sealed record CalculatorInputView(
    string Code,
    string LabelAr,
    string LabelEn,
    string? HintAr,
    string? HintEn,
    CalculatorInputType Type,
    bool Required,
    IReadOnlyList<CalculatorOption> Options,
    decimal? Min,
    decimal? Max,
    bool Prefilled);

internal static partial class CalculatorRules
{
    private const int MaxInputs = 40;
    private const int MaxResults = 20;

    public static void Validate(CalculatorDefinition calculator, JsonLogicEvaluator evaluator, ValidationErrors errors)
    {
        errors.Required("calculatorTitleAr", calculator.TitleAr);
        errors.Required("calculatorTitleEn", calculator.TitleEn);
        if (calculator.Inputs.Count is 0 or > MaxInputs)
        {
            errors.Add("calculatorInputs", $"A calculator asks for 1 to {MaxInputs} fields.");
        }

        if (calculator.Results.Count is 0 or > MaxResults)
        {
            errors.Add("calculatorResults", $"A calculator shows 1 to {MaxResults} results.");
        }

        foreach (var duplicate in calculator.Inputs.GroupBy(i => i.Code).Where(g => g.Count() > 1))
        {
            errors.Add("calculatorInputs", $"'{duplicate.Key}' is used more than once.");
        }

        for (var i = 0; i < calculator.Inputs.Count; i++)
        {
            var input = calculator.Inputs[i];
            var at = $"calculatorInputs[{i}]";
            if (!Name().IsMatch(input.Code ?? string.Empty))
            {
                errors.Add(at + ".code", "Use letters, digits and _, starting with a letter.");
            }

            errors.Required(at + ".labelAr", input.LabelAr);
            errors.Required(at + ".labelEn", input.LabelEn);
            if (input.Type == CalculatorInputType.Select && input.Options.Count == 0)
            {
                errors.Add(at + ".options", "A choice field needs its choices.");
            }

            if (input.Min > input.Max)
            {
                errors.Add(at + ".max", "Must not be less than the least.");
            }

            if (input.Prefill is not null)
            {
                try
                {
                    evaluator.Evaluate(input.Prefill, new JsonObject { ["party"] = new JsonObject() });
                }
                catch (JsonLogicException ex)
                {
                    errors.Add(at + ".prefill", $"Invalid rule: {ex.Message}");
                }
            }
        }

        for (var r = 0; r < calculator.Results.Count; r++)
        {
            errors.Required($"calculatorResults[{r}].path", calculator.Results[r].Path);
            errors.Required($"calculatorResults[{r}].labelAr", calculator.Results[r].LabelAr);
            errors.Required($"calculatorResults[{r}].labelEn", calculator.Results[r].LabelEn);
        }
    }

    /// <summary>Only the calculator's own fields reach the rule set, numbers read as numbers.</summary>
    public static JsonObject Clean(CalculatorDefinition calculator, JsonObject input)
    {
        var clean = new JsonObject();
        foreach (var field in calculator.Inputs)
        {
            var value = input[field.Code];
            if (value is null)
            {
                continue;
            }

            if (field.Type == CalculatorInputType.Number && value is JsonValue text && text.GetValueKind() == JsonValueKind.String)
            {
                if (decimal.TryParse(text.GetValue<string>(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    clean[field.Code] = number;
                }

                continue;
            }

            clean[field.Code] = value.DeepClone();
        }

        return clean;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex Name();
}
