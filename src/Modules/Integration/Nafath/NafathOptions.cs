namespace DynamicCube.Modules.Integration.Nafath;

/// <summary>
/// Nafath on this installation (ADR-0043, configuration <c>Nafath</c>): Elm's Rabet API address for the environment
/// (sandbox <c>…/nafath-sandbox</c>, staging <c>…/stg</c>, production without a prefix), the name of the credentials in
/// the secret store (<c>Secrets:{name}:appId</c> and <c>appKey</c>; never in the database), the server's address Elm
/// allowlists, and the sign-in bridge Keycloak uses.
/// </summary>
public sealed class NafathOptions
{
    public const string Section = "Nafath";

    /// <summary>Rabet's address with the environment's prefix; empty: Nafath is not set up.</summary>
    public string? BaseUrl { get; set; }

    public string SecretName { get; set; } = "NAFATH";

    /// <summary>The server's outbound address, sent after the person's address in <c>X-Forwarded-For</c>.</summary>
    public string? ServerIp { get; set; }

    public string Locale { get; set; } = "ar";

    /// <summary>Nafath's service a verification in the app is for, unless the caller names one.</summary>
    public string VerificationService { get; set; } = "OpenAccount";

    /// <summary>How long Nafath keeps a request open.</summary>
    public int RequestSeconds { get; set; } = 200;

    public NafathBridgeOptions Bridge { get; set; } = new();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}

/// <summary>
/// The sign-in bridge: an OpenID Connect provider Keycloak signs applicants in with, that sends them through Nafath's web
/// sign-in. Its client secret and signing key are in the secret store (<c>Secrets:{name}:clientSecret</c> and
/// <c>signingKey</c>, a PEM RSA key).
/// </summary>
public sealed class NafathBridgeOptions
{
    /// <summary>The bridge's issuer as browsers and Keycloak see it; empty: taken from the request.</summary>
    public string? Issuer { get; set; }

    public string ClientId { get; set; } = "keycloak";

    public string SecretName { get; set; } = "NAFATH_BRIDGE";

    /// <summary>Where the bridge may send people back (Keycloak's broker endpoint of the identity provider).</summary>
    public List<string> RedirectUris { get; set; } = [];
}
