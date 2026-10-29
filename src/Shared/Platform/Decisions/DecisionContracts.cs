using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Decisions;

/// <summary>What an engine returns: the results and how it got there (shown to reviewers, kept for appeals).</summary>
public sealed record EngineResult(JsonObject Output, JsonArray Trace);

/// <summary>
/// A decision engine (ADR-0014). The platform keeps rule-set configuration versioned and approved, maps inputs and
/// outputs and stores every evaluation; the engine only computes. Engines are stateless, so the same configuration can
/// also power a public calculator. Implementations register as <c>IDecisionEngine</c> services.
/// </summary>
public interface IDecisionEngine
{
    /// <summary>Stable code a rule set names its engine by, e.g. <c>RULES</c>.</summary>
    string Code { get; }

    string NameAr { get; }

    string NameEn { get; }

    /// <summary>Problems with a configuration (field path → message); empty when it can be used.</summary>
    IReadOnlyDictionary<string, string> Validate(JsonNode? configuration);

    Task<EngineResult> EvaluateAsync(JsonNode configuration, JsonObject input, CancellationToken cancellationToken);
}

/// <param name="Subject">What the decision is about (e.g. <c>SERVICE_REQUEST</c>) and its id and number, for the evaluation log.</param>
public sealed record DecisionRequest(string RuleSetCode, JsonObject Input, string SubjectType, Guid SubjectId, string? SubjectNumber);

/// <summary>A recorded evaluation: the rule-set version used, the results and the trace.</summary>
public sealed record DecisionResult(Guid EvaluationId, string RuleSetCode, int Version, string Engine, JsonObject Output, JsonArray Trace);

/// <summary>Evaluates a rule set's current published version and records the evaluation (implemented by the Decisions module).</summary>
public interface IDecisionService
{
    Task<DecisionResult> EvaluateAsync(DecisionRequest request, CancellationToken cancellationToken);

    /// <summary>Whether the rule set exists, is active and has a published version in use (so it can be evaluated).</summary>
    Task<bool> IsReadyAsync(string ruleSetCode, CancellationToken cancellationToken);
}

/// <summary>The decision cannot be made: no such rule set or version, unknown engine, or the engine failed.</summary>
public sealed class DecisionException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Where rule sets are used, so a rule set cannot be retired or deactivated while a published workflow still decides with
/// it (implemented by the Workflow module).
/// </summary>
public interface IRuleSetUsage
{
    /// <summary>Published, not retired workflow versions (<c>CODE/N</c>) with a decision step that uses the rule set.</summary>
    Task<IReadOnlyList<string>> WorkflowsUsingAsync(string ruleSetCode, CancellationToken cancellationToken);
}
