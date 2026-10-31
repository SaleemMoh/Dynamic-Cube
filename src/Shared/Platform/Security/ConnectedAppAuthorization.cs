namespace DynamicCube.Platform.Security;

/// <summary>
/// Connected apps (ADR-0036 step 7): outside systems whose users sign in through the platform's identity provider. Their
/// tokens carry the connected-app audience instead of the internal API's, so they are accepted only by the endpoints made
/// for connected apps (this policy), never by the rest of the internal API.
/// </summary>
public static class ConnectedAppAuthorization
{
    /// <summary>Authorization policy of the connected-app endpoints.</summary>
    public const string Policy = "connected-app";

    /// <summary>Audience of connected apps' tokens (configuration <c>Authentication:Internal:ConnectAudience</c>).</summary>
    public const string DefaultAudience = "dynamic-cube-connect-api";

    /// <summary>Whether the token was issued for connected apps only (it may then call only their endpoints).</summary>
    public static bool IsConnectedOnly(System.Security.Claims.ClaimsPrincipal user, string? mainAudience, string connectAudience)
    {
        ArgumentNullException.ThrowIfNull(user);
        var audiences = user.FindAll("aud").Select(c => c.Value).ToList();
        return audiences.Contains(connectAudience, StringComparer.Ordinal) && (mainAudience is null || !audiences.Contains(mainAudience, StringComparer.Ordinal));
    }
}
