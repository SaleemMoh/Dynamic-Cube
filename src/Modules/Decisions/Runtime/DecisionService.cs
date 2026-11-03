using System.Diagnostics;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Decisions.Domain;
using DynamicCube.Modules.Decisions.Persistence;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Decisions.Runtime;

public sealed record EvaluationView(
    Guid Id,
    string RuleSetCode,
    int Version,
    string Engine,
    string SubjectType,
    Guid SubjectId,
    string? SubjectNumber,
    DateTimeOffset At,
    string By,
    int DurationMs,
    JsonObject Input,
    JsonObject Output,
    JsonArray Trace);

public sealed record TryRequest(JsonObject? Input, int? Version);

public sealed record EngineInfo(string Code, string NameAr, string NameEn);

/// <summary>
/// Evaluates rule sets: the current published version (or a given one, when trying it out), through its engine, recording
/// inputs, results and trace. Public rule sets also answer anonymous calculator requests, which are not recorded.
/// </summary>
internal sealed class DecisionService(DecisionsDbContext context, IEnumerable<IDecisionEngine> engines, ICurrentUser user, TimeProvider clock, JsonLogicEvaluator evaluator) : IDecisionService
{
    public const string TrySubject = "TRY";

    public async Task<DecisionResult> EvaluateAsync(DecisionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleSet ruleSet;
        RuleSetVersion version;
        try
        {
            (ruleSet, version) = await LoadAsync(request.RuleSetCode, null, cancellationToken);
        }
        catch (DecisionNotFoundException ex)
        {
            // Callers of the contract (workflow decision steps) know only DecisionException.
            throw new DecisionException(ex.Message, ex);
        }

        return await RunAsync(ruleSet, version, request.Input, request.SubjectType, request.SubjectId, request.SubjectNumber, cancellationToken);
    }

    public async Task<DecisionPreview> PreviewAsync(string ruleSetCode, JsonObject input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        RuleSet ruleSet;
        RuleSetVersion version;
        try
        {
            (ruleSet, version) = await LoadAsync(ruleSetCode, null, cancellationToken);
        }
        catch (DecisionNotFoundException ex)
        {
            throw new DecisionException(ex.Message, ex);
        }

        var result = await EngineAsync(ruleSet, version, input, cancellationToken);
        return new DecisionPreview(ruleSet.Code, version.Number, result.Output);
    }

    public Task<bool> IsReadyAsync(string ruleSetCode, CancellationToken cancellationToken) =>
        context.Versions.AsNoTracking().AnyAsync(v => v.RuleSet!.Code == ruleSetCode && v.RuleSet.IsActive && !v.IsRetired, cancellationToken);

    /// <summary>Tries a version out (the test console); recorded as a <c>TRY</c> evaluation.</summary>
    public async Task<DecisionResult> TryAsync(string code, TryRequest request, CancellationToken cancellationToken)
    {
        var (ruleSet, version) = await LoadAsync(code, request.Version, cancellationToken);
        return await RunAsync(ruleSet, version, request.Input ?? [], TrySubject, Guid.Empty, null, cancellationToken);
    }

    /// <summary>A public calculator: only rule sets marked public, nothing recorded, no trace.</summary>
    public async Task<JsonObject> CalculateAsync(string code, JsonObject input, CancellationToken cancellationToken)
    {
        var (ruleSet, version) = await LoadAsync(code, null, cancellationToken);
        if (!ruleSet.IsPublic)
        {
            throw new DecisionNotFoundException($"No public calculator '{code}'.");
        }

        // A described calculator passes on only its own fields.
        if (CalculatorDefinition.Parse(ruleSet.Calculator) is { } calculator)
        {
            input = CalculatorRules.Clean(calculator, input);
        }

        return (await EngineAsync(ruleSet, version, input, cancellationToken)).Output;
    }

    /// <summary>The public calculators shown on the portal (public, active rule sets described as one).</summary>
    public async Task<IReadOnlyList<CalculatorSummary>> CalculatorsAsync(CancellationToken cancellationToken)
    {
        var rows = await context.RuleSets.AsNoTracking().Where(r => r.IsPublic && r.IsActive && r.Calculator != null).OrderBy(r => r.Code).ToListAsync(cancellationToken);
        return [.. rows.Select(r => (r.Code, Calculator: CalculatorDefinition.Parse(r.Calculator)!))
            .Select(r => new CalculatorSummary(r.Code, r.Calculator.TitleAr, r.Calculator.TitleEn, r.Calculator.IntroAr, r.Calculator.IntroEn, r.Calculator.Icon))];
    }

    /// <summary>A public calculator's form and results.</summary>
    public async Task<CalculatorView> CalculatorAsync(string code, CancellationToken cancellationToken)
    {
        var (ruleSet, calculator) = await PublicCalculatorAsync(code, cancellationToken);
        return new CalculatorView(
            ruleSet.Code,
            calculator.TitleAr,
            calculator.TitleEn,
            calculator.IntroAr,
            calculator.IntroEn,
            calculator.Icon,
            [.. calculator.Inputs.Select(i => new CalculatorInputView(i.Code, i.LabelAr, i.LabelEn, i.HintAr, i.HintEn, i.Type, i.Required, i.Options, i.Min, i.Max, i.Prefill is not null))],
            calculator.Results,
            calculator.NoteAr,
            calculator.NoteEn);
    }

    /// <summary>Starting values from the party's latest data (each field's prefill rule over <c>{ party }</c>).</summary>
    public async Task<JsonObject> PrefillAsync(string code, JsonObject party, CancellationToken cancellationToken)
    {
        var (_, calculator) = await PublicCalculatorAsync(code, cancellationToken);
        var facts = new JsonObject { ["party"] = party };
        var values = new JsonObject();
        foreach (var input in calculator.Inputs.Where(i => i.Prefill is not null))
        {
            try
            {
                if (evaluator.Evaluate(input.Prefill, facts) is { } value)
                {
                    values[input.Code] = value.DeepClone();
                }
            }
            catch (JsonLogicException)
            {
                // A value that cannot be worked out is left for the applicant to type.
            }
        }

        return values;
    }

    private async Task<(RuleSet RuleSet, CalculatorDefinition Calculator)> PublicCalculatorAsync(string code, CancellationToken cancellationToken)
    {
        var ruleSet = await context.RuleSets.AsNoTracking().SingleOrDefaultAsync(r => r.Code == code, cancellationToken);
        return ruleSet is { IsPublic: true, IsActive: true } && CalculatorDefinition.Parse(ruleSet.Calculator) is { } calculator
            ? (ruleSet, calculator)
            : throw new DecisionNotFoundException($"No public calculator '{code}'.");
    }

    public async Task<IReadOnlyList<EvaluationView>> ListAsync(string? subjectType, Guid? subjectId, string? ruleSet, int take, CancellationToken cancellationToken)
    {
        var query = context.Evaluations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(subjectType)) query = query.Where(e => e.SubjectType == subjectType);
        if (subjectId is not null) query = query.Where(e => e.SubjectId == subjectId);
        if (!string.IsNullOrWhiteSpace(ruleSet)) query = query.Where(e => e.RuleSetCode == ruleSet);
        var rows = await query.OrderByDescending(e => e.At).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        return [.. rows.Select(View)];
    }

    public IReadOnlyList<EngineInfo> Engines() => [.. engines.Select(e => new EngineInfo(e.Code, e.NameAr, e.NameEn))];

    private async Task<DecisionResult> RunAsync(RuleSet ruleSet, RuleSetVersion version, JsonObject input, string subjectType, Guid subjectId, string? subjectNumber, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var result = await EngineAsync(ruleSet, version, input, cancellationToken);
        var evaluation = new Evaluation
        {
            RuleSetCode = ruleSet.Code,
            Version = version.Number,
            Engine = ruleSet.Engine,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectNumber = subjectNumber,
            Input = input.ToJsonString(),
            Output = result.Output.ToJsonString(),
            Trace = result.Trace.ToJsonString(),
            At = clock.GetUtcNow(),
            By = user.UserName ?? user.UserId,
            DurationMs = (int)watch.ElapsedMilliseconds,
        };
        context.Evaluations.Add(evaluation);
        await context.SaveChangesAsync(cancellationToken);
        return new DecisionResult(evaluation.Id, ruleSet.Code, version.Number, ruleSet.Engine, result.Output, result.Trace);
    }

    private async Task<EngineResult> EngineAsync(RuleSet ruleSet, RuleSetVersion version, JsonObject input, CancellationToken cancellationToken)
    {
        var engine = engines.FirstOrDefault(e => e.Code == ruleSet.Engine) ?? throw new DecisionException($"Engine '{ruleSet.Engine}' of rule set '{ruleSet.Code}' is not installed.");
        try
        {
            return await engine.EvaluateAsync(JsonNode.Parse(version.Configuration)!, input, cancellationToken);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or DecisionException))
        {
            throw new DecisionException($"Rule set {version.Code} failed: {ex.Message}", ex);
        }
    }

    private async Task<(RuleSet RuleSet, RuleSetVersion Version)> LoadAsync(string code, int? number, CancellationToken cancellationToken)
    {
        var ruleSet = await context.RuleSets.AsNoTracking().SingleOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (ruleSet is null || !ruleSet.IsActive)
        {
            throw new DecisionNotFoundException($"Rule set '{code}' does not exist or is not active.");
        }

        var version = await context.Versions.AsNoTracking()
            .Where(v => v.RuleSetId == ruleSet.Id && (number == null ? !v.IsRetired : v.Number == number))
            .OrderByDescending(v => v.Number).FirstOrDefaultAsync(cancellationToken)
            ?? throw new DecisionNotFoundException(number is null ? $"Rule set '{code}' has no published version in use." : $"Rule set '{code}' has no version {number}.");
        return (ruleSet, version);
    }

    private static EvaluationView View(Evaluation e) => new(
        e.Id, e.RuleSetCode, e.Version, e.Engine, e.SubjectType, e.SubjectId, e.SubjectNumber, e.At, e.By, e.DurationMs,
        JsonNode.Parse(e.Input)!.AsObject(), JsonNode.Parse(e.Output)!.AsObject(), JsonNode.Parse(e.Trace)!.AsArray());
}

public sealed class DecisionNotFoundException(string message) : Exception(message);
