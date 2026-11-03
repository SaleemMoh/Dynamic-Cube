using System.Text.Json.Nodes;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Decisions.Runtime.Matrix;

/// <summary>A matrix tried in the designer before it is saved: its configuration and the values to grade.</summary>
public sealed record MatrixSimulation(JsonObject Configuration, JsonObject? Input);

/// <summary>What the designer reads back: the problems with the configuration, or the result and its trace.</summary>
public sealed record MatrixSimulationResult(IReadOnlyDictionary<string, string> Errors, JsonObject? Output, JsonArray? Trace);

/// <summary>The matrix designer's server side: importing a classification guide and simulating an unsaved matrix.</summary>
internal sealed class MatrixDesign(JsonLogicEvaluator logic, TimeProvider clock)
{
    public static JsonObject Import(JsonObject export)
    {
        // An export may come wrapped as the reviewed system returns it ({ succeeded, data }).
        var guide = export["data"] is JsonObject data && export.ContainsKey("succeeded") ? data : export;
        if (guide["sectors"] is not JsonArray)
        {
            throw new DecisionException("This file is not a classification guide export (no sectors).");
        }

        var result = GuideImport.Convert(guide);
        return new JsonObject
        {
            ["configuration"] = result.Definition.ToJson(),
            ["extras"] = result.Extras,
            ["warnings"] = new JsonArray(result.Warnings.Select(w => (JsonNode?)w).ToArray()),
        };
    }

    public async Task<MatrixSimulationResult> SimulateAsync(MatrixSimulation simulation, CancellationToken cancellationToken)
    {
        var engine = new MatrixEngine(logic, clock);
        var errors = engine.Validate(simulation.Configuration);
        if (errors.Count > 0)
        {
            return new MatrixSimulationResult(errors, null, null);
        }

        var result = await engine.EvaluateAsync(simulation.Configuration, simulation.Input ?? [], cancellationToken);
        return new MatrixSimulationResult(errors, result.Output, result.Trace);
    }
}
