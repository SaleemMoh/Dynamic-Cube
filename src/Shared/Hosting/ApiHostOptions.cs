namespace DynamicCube.Hosting;

/// <summary>Identifies an API host and the Keycloak realm it trusts.</summary>
public sealed class ApiHostOptions
{
    /// <summary><c>INTERNAL</c> or <c>EXTERNAL</c>.</summary>
    public required string AppCode { get; init; }

    /// <summary>Configuration section with <c>Authority</c>, <c>Audience</c> and <c>RequireHttpsMetadata</c>.</summary>
    public required string AuthenticationSection { get; init; }

    /// <summary>
    /// The host serves the public API (ADR-0042): it also accepts tokens with the public API's audience
    /// (<c>PublicApi:Audience</c>), which reach only the public API's endpoints.
    /// </summary>
    public bool PublicApi { get; init; }
}
