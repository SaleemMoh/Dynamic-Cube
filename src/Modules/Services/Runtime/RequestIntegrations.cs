using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// The outcome of an integration step, kept on the request so a required one is checked again on submit and the applicant
/// sees it: <paramref name="Values"/> are the outputs the step shows (by output), for its result card.
/// </summary>
public sealed record IntegrationStepRun(bool Success, string? Code, string? MessageAr, string? MessageEn, DateTimeOffset At, Dictionary<string, JsonNode?>? Values = null);

/// <summary>An answer that changed when a draft's integrations were read again (shown to the applicant: from → to).</summary>
public sealed record AnswerChange(string Question, string LabelAr, string LabelEn, JsonNode? From, JsonNode? To);

/// <summary>
/// Integration Manager calls made for a request: questions answered by an integration (filled when the draft starts, and
/// kept equal to the integration when the applicant may not change them) and integration steps (called when the
/// applicant moves on). Inputs are paths in the request's facts (<c>party…</c>, <c>applicant…</c>, <c>data…</c>).
/// </summary>
internal sealed class RequestIntegrations(IServiceProvider services, TimeProvider clock)
{
    /// <summary>
    /// Answers questions filled by an integration: every empty one when the draft starts; afterwards only the locked
    /// ones, which always hold the integration's value (or nothing when it gives none).
    /// </summary>
    public async Task FillAsync(ResolvedVersion version, JsonObject context, JsonObject data, bool lockedOnly, CancellationToken cancellationToken)
    {
        var calls = new Dictionary<string, IntegrationResult?>(StringComparer.Ordinal);
        foreach (var question in version.Questions.Values.Where(q => q.Integration is not null))
        {
            if (lockedOnly ? !question.Locked : data[question.Code] is not null)
            {
                continue;
            }

            var fill = question.Integration!;
            var inputs = Inputs(fill.Inputs, context);
            var key = fill.Operation + "|" + JsonSerializer.Serialize(inputs);
            if (!calls.TryGetValue(key, out var result))
            {
                calls[key] = result = (await CallAsync(fill.Operation, inputs, cancellationToken)).Result;
            }

            var value = result is { IsSuccess: true } ? Output(result, fill.Output) : null;
            if (value is not null)
            {
                data[question.Code] = value;
            }
            else if (question.Locked)
            {
                data.Remove(question.Code);
            }
        }
    }

    /// <summary>Calls an integration step's operation and puts its outputs into the answers.</summary>
    public async Task<IntegrationStepRun> RunStepAsync(IntegrationStepDefinition step, JsonObject context, JsonObject data, CancellationToken cancellationToken)
    {
        var (result, problem) = await CallAsync(step.Operation, Inputs(step.Inputs, context), cancellationToken);
        if (result is not { IsSuccess: true })
        {
            return new IntegrationStepRun(
                false,
                result?.Code,
                result?.MessageAr ?? problem.Ar,
                result?.MessageEn ?? problem.En,
                clock.GetUtcNow());
        }

        foreach (var output in step.Outputs)
        {
            if (Output(result, output.Output) is { } value)
            {
                data[output.Question] = value;
            }
            else
            {
                data.Remove(output.Question);
            }
        }

        var values = step.Display.ToDictionary(d => d.Output, d => Output(result, d.Output), StringComparer.Ordinal);
        return new IntegrationStepRun(true, result.Code, result.MessageAr, result.MessageEn, clock.GetUtcNow(), values);
    }

    /// <summary>
    /// Reads a draft's integrations again when it is opened (providers' data may have changed since): the integration steps
    /// already run and the locked answers. Returns the answers that changed, from → to.
    /// </summary>
    public async Task<IReadOnlyList<AnswerChange>> RefreshAsync(ResolvedVersion version, JsonObject context, JsonObject data, JsonObject runs, CancellationToken cancellationToken)
    {
        var before = data.DeepClone().AsObject();
        await FillAsync(version, context, data, lockedOnly: true, cancellationToken);
        foreach (var step in version.Definition.Steps.Where(s => s.Type == StepType.Integration && s.Integration is not null && runs.ContainsKey(s.Code)))
        {
            var run = await RunStepAsync(step.Integration!, context, data, cancellationToken);
            runs[step.Code] = JsonSerializer.SerializeToNode(run, Platform.Configuration.ChangeJson.Options);
        }

        var changes = new List<AnswerChange>();
        foreach (var code in before.Select(p => p.Key).Union(data.Select(p => p.Key)).Distinct(StringComparer.Ordinal))
        {
            var from = before[code];
            var to = data[code];
            if (!JsonNode.DeepEquals(from, to) && version.Questions.TryGetValue(code, out var question))
            {
                changes.Add(new AnswerChange(code, question.LabelAr, question.LabelEn, from?.DeepClone(), to?.DeepClone()));
            }
        }

        return changes;
    }

    private static Dictionary<string, string?> Inputs(List<IntegrationInput> inputs, JsonObject context) =>
        inputs.ToDictionary(i => i.Parameter, i => Text(JsonPaths.Read(context, i.From)), StringComparer.Ordinal);

    private static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        _ => node.ToJsonString(),
    };

    /// <summary>An output by its configured name, else a path in the provider's result.</summary>
    private static JsonNode? Output(IntegrationResult result, string output) =>
        result.Outputs.TryGetValue(output, out var named) ? named?.DeepClone() : JsonPaths.Read(result.Data, output);

    private async Task<(IntegrationResult? Result, (string Ar, string En) Problem)> CallAsync(string operation, Dictionary<string, string?> inputs, CancellationToken cancellationToken)
    {
        if (services.GetService<IIntegrationGateway>() is not { } gateway)
        {
            return (null, ("خدمة التكامل غير متاحة.", "Integrations are not available."));
        }

        try
        {
            return (await gateway.InvokeAsync(operation, inputs, cancellationToken), default);
        }
        catch (IntegrationOperationNotFoundException)
        {
            return (null, ("عملية التكامل غير متاحة حاليًا.", "The integration operation is not available."));
        }
        catch (IntegrationInputException)
        {
            return (null, ("بيانات غير مكتملة للتحقق؛ أكمل الحقول المطلوبة.", "Some data needed for the check is missing; complete the required fields."));
        }
    }
}
