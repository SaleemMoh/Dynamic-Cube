using System.Text.Json.Nodes;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>The live result of a request (ADR-0040): the rule set version used and its outputs. Not recorded, not the decision.</summary>
public sealed record LiveResultView(string Step, string RuleSetCode, int Version, JsonObject Output);

/// <summary>What the live result of the service builder's preview is computed from: the decision step, the answers and the level.</summary>
public sealed record LiveResultPreviewRequest(string Service, string Step, JsonObject? Data, string? LevelCode);

/// <summary>
/// The live result beside the applicant's form (ADR-0040): the decision step of the service's workflow — its rule set and its
/// input mapping — evaluated on the request's facts as they are now, without recording anything. Using the workflow's own
/// step means the preview is computed exactly as the decision will be.
/// </summary>
internal sealed class LiveResult(IServiceProvider services, JsonLogicEvaluator logic)
{
    public async Task<LiveResultView> EvaluateAsync(string serviceCode, string step, JsonObject facts, CancellationToken cancellationToken)
    {
        var workflows = services.GetService<IServiceWorkflows>() ?? throw new RequestConflictException("Workflows are not installed.");
        var decisions = services.GetService<IDecisionService>() ?? throw new RequestConflictException("Decision rules are not installed.");
        var workflow = await workflows.WorkflowOfAsync(serviceCode, cancellationToken) ?? throw new RequestConflictException("The service has no workflow.");
        var definition = await workflows.LatestDefinitionAsync(workflow, cancellationToken)
            ?? throw new RequestConflictException("The service's workflow has no published version.");
        var node = (JsonNode.Parse(definition)?["nodes"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(n => n["code"]?.GetValue<string>() == step && string.Equals(n["type"]?.GetValue<string>(), "DECISION", StringComparison.OrdinalIgnoreCase))
            ?? throw new RequestConflictException($"The service's workflow has no decision step '{step}'.");
        var ruleSet = node["ruleSet"]?.GetValue<string>() ?? throw new RequestConflictException($"Decision step '{step}' names no rule set.");
        var input = node["input"] is JsonObject map && map.Count > 0 ? Map(map, facts) : facts;
        try
        {
            var preview = await decisions.PreviewAsync(ruleSet, input, cancellationToken);
            return new LiveResultView(step, preview.RuleSetCode, preview.Version, preview.Output);
        }
        catch (DecisionException ex)
        {
            throw new RequestConflictException(ex.Message);
        }
    }

    /// <summary>The step's input: each parameter's rule evaluated over the facts (as the workflow does).</summary>
    private JsonObject Map(JsonObject map, JsonObject facts)
    {
        var input = new JsonObject();
        foreach (var (name, rule) in map)
        {
            input[name] = logic.Evaluate(rule, facts)?.DeepClone();
        }

        return input;
    }
}
