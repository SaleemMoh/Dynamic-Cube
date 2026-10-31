using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Integration;

namespace DynamicCube.Modules.Services.Definitions;

/// <summary>Checks the inputs of an integration call: each names a parameter and a path in the request's facts.</summary>
internal static class IntegrationInputs
{
    public static void Check(List<IntegrationInput> inputs, string field, ValidationErrors errors)
    {
        ChildSync.RequireUnique(inputs, i => i.Parameter, field, errors);
        foreach (var (input, i) in inputs.Select((x, i) => (x, i)))
        {
            input.Parameter = input.Parameter?.Trim() ?? string.Empty;
            input.From = input.From?.Trim() ?? string.Empty;
            errors.Required($"{field}[{i}].parameter", input.Parameter);
            if (!JsonPaths.IsValid(input.From))
            {
                errors.Add($"{field}[{i}].from", "Give a path such as party.identifiers.CR_NUMBER or data.REGION.");
            }
        }
    }
}
