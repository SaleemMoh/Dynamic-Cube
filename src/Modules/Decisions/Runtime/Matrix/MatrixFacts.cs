using System.Text.Json.Nodes;
using DynamicCube.Platform.Integration;

namespace DynamicCube.Modules.Decisions.Runtime.Matrix;

/// <summary>
/// Reads a matrix's inputs from a request's facts, as a workflow decision step (or the live panel, or a task's
/// recalculation) passes them: <c>data</c> (answers), <c>inputs</c> (what employees filled in on tasks),
/// <c>integrations</c> (workflow integration steps), <c>party</c> and the rest. Each input is read from its source:
/// <list type="bullet">
/// <item><c>applicant</c>: <c>data.&lt;question&gt;</c> (the input's code when no question is named);</item>
/// <item><c>reviewer</c>: <c>inputs.&lt;step&gt;.&lt;field&gt;</c> (any task with that field when no step is named);
/// missing means the cell waits for the review;</item>
/// <item><c>integration</c>: the output of the named workflow step, or of the last step that called the operation, or the
/// answer a form integration step filled (<c>question</c>);</item>
/// <item><c>party</c>: the party fact at <c>path</c> (<c>party.profile.X</c>, <c>party.identifiers.CR_NUMBER</c>…);</item>
/// <item><c>given</c>: the fact at <c>path</c> (or a top-level fact named as the input).</item>
/// </list>
/// <c>derived</c> and <c>projects</c> inputs are worked out by the engine. When an input allows it, a value an employee
/// entered on a task under the input's code replaces the one read.
/// </summary>
public static class MatrixFacts
{
    /// <summary>Whether the input is a request's facts (rather than the values themselves).</summary>
    public static bool IsFacts(JsonObject input) =>
        input["values"] is null && (input["data"] is JsonObject || input["party"] is JsonObject || input["inputs"] is JsonObject);

    /// <summary>The value of every input that comes from outside, and where each one was looked for.</summary>
    public static (JsonObject Values, JsonArray Sources) Resolve(MatrixDefinition definition, JsonObject facts)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(facts);
        var values = new JsonObject();
        var sources = new JsonArray();
        foreach (var input in definition.Inputs)
        {
            var computed = input.Source.Kind is "derived" or "projects";
            var (value, from) = computed ? (null, null) : Read(input, facts);
            if (input.Source.AllowOverride && Override(facts, input.Code) is { } entered)
            {
                (value, from) = entered;
            }

            if (computed && value is null)
            {
                continue;
            }

            values[input.Code] = value?.DeepClone();
            sources.Add(new JsonObject { ["input"] = input.Code, ["kind"] = input.Source.Kind, ["from"] = from, ["found"] = value is not null });
        }

        return (values, sources);
    }

    /// <summary>The rows an answer of the rows input selects (by each row's answer, or its code), ignoring case.</summary>
    public static HashSet<string> Rows(MatrixDefinition definition, JsonNode? answer)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var chosen = answer switch
        {
            JsonArray list => list.Select(Text).Where(t => t is not null).Select(t => t!).ToList(),
            JsonValue value when Text(value) is { } one => one.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            _ => [],
        };
        return definition.Rows
            .Where(r => chosen.Any(c => string.Equals(c, r.Answer ?? r.Code, StringComparison.OrdinalIgnoreCase) || string.Equals(c, r.Code, StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? Text(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonObject o => (o["code"] ?? o["value"]) is JsonValue c && c.TryGetValue<string>(out var code) ? code : null,
        JsonValue v => v.ToJsonString(),
        _ => null,
    };

    private static (JsonNode? Value, string? From) Read(MatrixInput input, JsonObject facts)
    {
        var s = input.Source;
        switch (s.Kind)
        {
            case "applicant":
                return At(facts, $"data.{s.Question ?? input.Code}");
            case "reviewer":
                var field = s.Field ?? input.Code;
                return s.Step is { Length: > 0 } step ? At(facts, $"inputs.{step}.{field}") : AnyTask(facts, field);
            case "integration":
                return Integration(s, input.Code, facts);
            case "party":
            case "given":
                return At(facts, s.Path ?? input.Code);
            default:
                return (null, null);
        }
    }

    private static (JsonNode? Value, string? From) Integration(MatrixSource s, string code, JsonObject facts)
    {
        var output = s.Output ?? code;
        if (s.Step is { Length: > 0 } step && At(facts, $"integrations.{step}.outputs.{output}") is { Value: not null } byStep)
        {
            return byStep;
        }

        if (s.Operation is { Length: > 0 } operation && facts["integrations"] is JsonObject calls)
        {
            // The last workflow step that called the operation and succeeded.
            foreach (var (name, call) in calls.Reverse())
            {
                if (call is JsonObject c
                    && string.Equals(c["operation"]?.ToString(), operation, StringComparison.OrdinalIgnoreCase)
                    && c["success"] is JsonValue ok && ok.TryGetValue<bool>(out var success) && success
                    && (c["outputs"] as JsonObject)?[output] is { } value)
                {
                    return (value, $"integrations.{name}.outputs.{output}");
                }
            }
        }

        // A form integration step keeps its outputs as answers.
        return s.Question is { Length: > 0 } question ? At(facts, $"data.{question}") : (null, s.Step is null ? $"operation {s.Operation}" : $"integrations.{s.Step}");
    }

    private static (JsonNode? Value, string? From) AnyTask(JsonObject facts, string field)
    {
        if (facts["inputs"] is JsonObject tasks)
        {
            foreach (var (task, filled) in tasks)
            {
                if ((filled as JsonObject)?[field] is { } value)
                {
                    return (value, $"inputs.{task}.{field}");
                }
            }
        }

        return (null, $"inputs.*.{field}");
    }

    /// <summary>A value an employee entered on a task under the input's own code.</summary>
    private static (JsonNode? Value, string? From)? Override(JsonObject facts, string code) =>
        AnyTask(facts, code) is { Value: not null } entered ? entered : null;

    private static (JsonNode? Value, string? From) At(JsonObject facts, string path) =>
        JsonPaths.IsValid(path) ? (Empty(JsonPaths.Read(facts, path)), path) : (null, path);

    /// <summary>An empty answer (blank text) counts as no value.</summary>
    private static JsonNode? Empty(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s) ? null : node;
}
