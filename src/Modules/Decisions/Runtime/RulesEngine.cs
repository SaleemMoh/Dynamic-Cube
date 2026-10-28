using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Decisions.Runtime;

/// <summary>
/// The built-in engine: named JSONLogic steps. Configuration:
/// <code>
/// {
///   "variables": [{ "code": "saudiPct", "rule": {"*": [100, {"/": [{"var": "input.saudis"}, {"var": "input.employees"}]}]} }],
///   "items": {                                   // optional: one result per element, e.g. per sector
///     "source": {"var": "input.sectors"}, "as": "sector",
///     "variables": [{ "code": "grade", "rule": {...} }],
///     "output": { "sector": {"var": "sector"}, "grade": {"var": "vars.grade"} }
///   },
///   "output": { "worstGrade": {"max": ...} }     // may read "items" (the item results)
/// }
/// </code>
/// Rules read <c>input</c>, <c>vars</c> (earlier steps) and, inside items, the element (by its <c>as</c> name) and
/// <c>index</c>. Every step's value goes to the trace.
/// </summary>
internal sealed partial class RulesEngine(JsonLogicEvaluator logic) : IDecisionEngine
{
    public const string EngineCode = "RULES";

    public string Code => EngineCode;

    public string NameAr => "قواعد JSONLogic";

    public string NameEn => "JSONLogic rules";

    public IReadOnlyDictionary<string, string> Validate(JsonNode? configuration)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (configuration is not JsonObject config)
        {
            errors[string.Empty] = "The configuration is a JSON object with variables, items and output.";
            return errors;
        }

        foreach (var key in config.Select(p => p.Key).Where(k => k is not ("variables" or "items" or "output")))
        {
            errors[key] = "Unknown property; use variables, items and output.";
        }

        CheckVariables("variables", config["variables"], errors);
        if (config["items"] is { } items)
        {
            if (items is not JsonObject itemConfig)
            {
                errors["items"] = "items is an object with source, as, variables and output.";
            }
            else
            {
                if (itemConfig["source"] is null)
                {
                    errors["items.source"] = "Give the rule that lists the elements.";
                }

                if (itemConfig["as"] is JsonNode name && (name.GetValueKind() != System.Text.Json.JsonValueKind.String || !Name().IsMatch(name.GetValue<string>()) || name.GetValue<string>() is "input" or "vars" or "index" or "items"))
                {
                    errors["items.as"] = "A name of letters, digits and _ other than input, vars, index and items.";
                }

                CheckVariables("items.variables", itemConfig["variables"], errors);
                CheckOutput("items.output", itemConfig["output"], errors, required: true);
            }
        }

        CheckOutput("output", config["output"], errors, required: config["items"] is null);
        return errors;
    }

    public Task<EngineResult> EvaluateAsync(JsonNode configuration, JsonObject input, CancellationToken cancellationToken)
    {
        var config = configuration.AsObject();
        var trace = new JsonArray();
        var vars = new JsonObject();
        var data = new JsonObject { ["input"] = input.DeepClone(), ["vars"] = vars };
        Steps(config["variables"], data, vars, "vars", trace);

        JsonArray? results = null;
        if (config["items"] is JsonObject items)
        {
            results = [];
            var name = items["as"]?.GetValue<string>() ?? "item";
            var source = logic.Evaluate(items["source"], data) as JsonArray ?? [];
            trace.Add(new JsonObject { ["step"] = "items", ["value"] = source.Count });
            for (var i = 0; i < source.Count; i++)
            {
                var itemVars = (JsonObject)vars.DeepClone();
                var itemData = new JsonObject { ["input"] = data["input"]!.DeepClone(), ["vars"] = itemVars, [name] = source[i]?.DeepClone(), ["index"] = i };
                Steps(items["variables"], itemData, itemVars, $"items[{i}].vars", trace);
                results.Add(Output(items["output"], itemData, $"items[{i}]", trace));
            }

            data["items"] = results.DeepClone();
        }

        var output = Output(config["output"], data, "output", trace);
        if (results is not null && !output.ContainsKey("items"))
        {
            output["items"] = results;
        }

        return Task.FromResult(new EngineResult(output, trace));
    }

    private void Steps(JsonNode? steps, JsonObject data, JsonObject vars, string path, JsonArray trace)
    {
        foreach (var step in steps as JsonArray ?? [])
        {
            var code = step!["code"]!.GetValue<string>();
            var value = logic.Evaluate(step["rule"], data);
            vars[code] = value?.DeepClone();
            trace.Add(new JsonObject { ["step"] = $"{path}.{code}", ["value"] = value?.DeepClone() });
        }
    }

    private JsonObject Output(JsonNode? fields, JsonObject data, string path, JsonArray trace)
    {
        var output = new JsonObject();
        foreach (var (field, rule) in fields as JsonObject ?? [])
        {
            var value = logic.Evaluate(rule, data);
            output[field] = value?.DeepClone();
            trace.Add(new JsonObject { ["step"] = $"{path}.{field}", ["value"] = value?.DeepClone() });
        }

        return output;
    }

    private static void CheckVariables(string path, JsonNode? steps, Dictionary<string, string> errors)
    {
        if (steps is null)
        {
            return;
        }

        if (steps is not JsonArray list)
        {
            errors[path] = "A list of { code, rule }.";
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
        {
            var code = list[i]?["code"];
            if (code is null || code.GetValueKind() != System.Text.Json.JsonValueKind.String || !Name().IsMatch(code.GetValue<string>()))
            {
                errors[$"{path}[{i}].code"] = "A name of letters, digits and _.";
            }
            else if (!seen.Add(code.GetValue<string>()))
            {
                errors[$"{path}[{i}].code"] = "Used twice.";
            }

            if (list[i]?["rule"] is null)
            {
                errors[$"{path}[{i}].rule"] = "Give the rule.";
            }
        }
    }

    private static void CheckOutput(string path, JsonNode? output, Dictionary<string, string> errors, bool required)
    {
        if (output is null)
        {
            if (required)
            {
                errors[path] = "Give at least one output field.";
            }

            return;
        }

        if (output is not JsonObject fields || (required && fields.Count == 0))
        {
            errors[path] = "An object of output field → rule.";
            return;
        }

        foreach (var (field, _) in fields.Where(f => !Name().IsMatch(f.Key)))
        {
            errors[$"{path}.{field}"] = "Output names use letters, digits and _.";
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$")]
    private static partial Regex Name();
}
