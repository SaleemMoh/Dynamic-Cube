using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Integration;

/// <summary>
/// Calls an operation configured in the Integration Manager. Used by the Party Registry, LOVs, service steps and
/// workflow nodes; callers never see URLs, credentials or provider envelopes, only the outcome and mapped outputs.
/// </summary>
public interface IIntegrationGateway
{
    Task<IntegrationResult> InvokeAsync(string operationCode, IReadOnlyDictionary<string, string?> inputs, CancellationToken cancellationToken);
}

public enum IntegrationOutcome
{
    Success,
    NotFound,
    BusinessError,
    TechnicalError,
}

/// <summary>Result of an integration call.</summary>
/// <param name="Code">Provider business code (e.g. <c>001-000001</c>) or HTTP status when the provider sends none.</param>
/// <param name="MessageAr">User-facing message from the operation's response code map, else the provider's message.</param>
/// <param name="Outputs">Values of the operation's configured outputs.</param>
/// <param name="Data">The provider's result (envelope removed).</param>
public sealed record IntegrationResult(
    IntegrationOutcome Outcome,
    string? Code,
    string? MessageAr,
    string? MessageEn,
    IReadOnlyDictionary<string, JsonNode?> Outputs,
    JsonNode? Data,
    Guid CallId,
    bool FromCache)
{
    public bool IsSuccess => Outcome == IntegrationOutcome.Success;
}

/// <summary>The operation does not exist or is inactive.</summary>
public sealed class IntegrationOperationNotFoundException(string operationCode)
    : Exception($"Integration operation '{operationCode}' does not exist or is inactive.")
{
    public string OperationCode { get; } = operationCode;
}

/// <summary>Inputs do not match the operation's parameters (missing required values or unknown names).</summary>
public sealed class IntegrationInputException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The integration inputs are invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
