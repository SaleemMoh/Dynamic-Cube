using System.Text.RegularExpressions;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.ConnectedApps;

/// <summary>Rules for connected apps' sign-in clients and the rows that belong to a connected app (ADR-0036 step 7).</summary>
internal static partial class ConnectedClients
{
    /// <summary>Clients of the platform and of Keycloak itself, which no connected app may take.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "account", "account-console", "admin-cli", "broker", "realm-management", "security-admin-console",
    };

    public static string DefaultClientId(string appCode) => "dc-app-" + appCode.Trim().ToLowerInvariant();

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,99}$")]
    public static partial Regex ClientIdPattern();

    public static bool IsPlatformClient(string clientId) =>
        Reserved.Contains(clientId) || clientId.StartsWith("dynamic-cube", StringComparison.Ordinal);

    /// <summary>An absolute http(s) address, optionally ending with <c>*</c> (Keycloak allows a wildcard only at the end).</summary>
    public static bool IsRedirectUri(string value)
    {
        var text = value.EndsWith('*') ? value[..^1] : value;
        return !text.Contains('*', StringComparison.Ordinal)
            && Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.Fragment);
    }

    /// <summary>The origins of the return addresses, for the browser calls a connected app makes after sign-in.</summary>
    public static List<string> OriginsOf(IEnumerable<string> redirectUris) =>
    [
        .. redirectUris
            .Select(u => Uri.TryCreate(u.TrimEnd('*'), UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Whether the code names a connected app.</summary>
    public static async Task<bool> IsConnectedAsync(AppsControlDbContext context, string? appCode, CancellationToken cancellationToken) =>
        appCode is not null && await context.BusinessApps.AsNoTracking().AnyAsync(a => a.Code == appCode && a.Kind == AppKind.Connected, cancellationToken);
}
