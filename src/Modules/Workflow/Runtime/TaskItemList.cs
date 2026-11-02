using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Definitions;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>The decision on one item of a task: APPROVED or REJECTED, and the reason (needed for a rejection).</summary>
public sealed record ItemDecision(string Key, string? Decision, string? Reason);

/// <summary>One item of a task as the employee sees it: its key, label and the decision so far.</summary>
public sealed record TaskItemView(string Key, string Label, string? Decision, string? Reason);

/// <summary>One element of a task's item list: its key, label and the element itself.</summary>
internal sealed record TaskItem(string Key, string Label, JsonNode? Element);

/// <summary>Reads the items a task decides one by one and checks the decisions taken on them.</summary>
internal static class TaskItemList
{
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    private const int MaxItems = 200;
    private const int MaxReason = 1000;

    /// <summary>The task's items from the request's facts, each with a unique key.</summary>
    public static List<TaskItem> Read(TaskItems items, JsonObject facts, JsonLogicEvaluator evaluator)
    {
        var name = items.As ?? "item";
        var list = evaluator.Evaluate(items.Source, facts) as JsonArray ?? [];
        var result = new List<TaskItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in list.Take(MaxItems))
        {
            var scope = (JsonObject)facts.DeepClone();
            scope[name] = element?.DeepClone();
            var key = Text(items.Key is null ? element : evaluator.Evaluate(items.Key, scope));
            if (string.IsNullOrWhiteSpace(key))
            {
                key = (result.Count + 1).ToString(CultureInfo.InvariantCulture);
            }

            // Two elements with the same key are told apart by their position.
            var unique = key;
            for (var n = 2; !seen.Add(unique); n++)
            {
                unique = $"{key}#{n}";
            }

            var label = items.Label is null ? key : Text(evaluator.Evaluate(items.Label, scope));
            result.Add(new TaskItem(unique, string.IsNullOrWhiteSpace(label) ? key : label!, element?.DeepClone()));
        }

        return result;
    }

    /// <param name="complete">The task moves on, so every item needs a decision and every rejection a reason.</param>
    public static List<ItemDecision> Clean(IReadOnlyList<TaskItem> items, IEnumerable<ItemDecision>? given, bool complete)
    {
        var byKey = (given ?? []).Where(d => d?.Key is not null).GroupBy(d => d.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var clean = new List<ItemDecision>();
        foreach (var item in items)
        {
            byKey.TryGetValue(item.Key, out var decision);
            var verdict = decision?.Decision?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(verdict))
            {
                if (complete)
                {
                    throw new WorkflowValidationException($"items.{item.Key}", $"{item.Label}: approve or reject it.");
                }

                continue;
            }

            if (verdict is not (Approved or Rejected))
            {
                throw new WorkflowValidationException($"items.{item.Key}", $"{item.Label}: the decision must be APPROVED or REJECTED.");
            }

            var reason = decision!.Reason?.Trim();
            if (reason?.Length > MaxReason)
            {
                throw new WorkflowValidationException($"items.{item.Key}", $"{item.Label}: the reason may have at most {MaxReason} characters.");
            }

            if (complete && verdict == Rejected && string.IsNullOrEmpty(reason))
            {
                throw new WorkflowValidationException($"items.{item.Key}", $"{item.Label}: give the reason for the rejection.");
            }

            clean.Add(new ItemDecision(item.Key, verdict, string.IsNullOrEmpty(reason) ? null : reason));
        }

        return clean;
    }

    /// <summary>What later rules and documents read: every item with its decision, and the approved and rejected ones.</summary>
    public static JsonObject Facts(IReadOnlyList<TaskItem> items, IReadOnlyList<ItemDecision> decisions)
    {
        var byKey = decisions.ToDictionary(d => d.Key, StringComparer.Ordinal);
        var list = new JsonArray();
        var approved = new JsonArray();
        var rejected = new JsonArray();
        var approvedItems = new JsonArray();
        foreach (var item in items)
        {
            byKey.TryGetValue(item.Key, out var decision);
            list.Add(new JsonObject
            {
                ["key"] = item.Key,
                ["label"] = item.Label,
                ["decision"] = decision?.Decision,
                ["reason"] = decision?.Reason,
                ["item"] = item.Element?.DeepClone(),
            });
            if (decision?.Decision == Approved)
            {
                approved.Add(item.Key);
                approvedItems.Add(item.Element?.DeepClone());
            }
            else if (decision?.Decision == Rejected)
            {
                rejected.Add(item.Key);
            }
        }

        return new JsonObject
        {
            ["list"] = list,
            ["approved"] = approved,
            ["rejected"] = rejected,
            ["approvedItems"] = approvedItems,
            ["approvedCount"] = approved.Count,
            ["rejectedCount"] = rejected.Count,
            ["total"] = items.Count,
        };
    }

    public static List<ItemDecision> Parse(string json) =>
        JsonSerializer.Deserialize<List<ItemDecision>>(json, JsonSerializerOptions.Web) ?? [];

    public static string Serialize(IReadOnlyList<ItemDecision> decisions) => JsonSerializer.Serialize(decisions, JsonSerializerOptions.Web);

    private static string? Text(JsonNode? value) => value switch
    {
        null => null,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        JsonValue v when v.GetValueKind() == JsonValueKind.Number => v.ToJsonString(),
        JsonValue v when v.GetValueKind() is JsonValueKind.True or JsonValueKind.False => v.ToJsonString(),
        _ => null,
    };
}
