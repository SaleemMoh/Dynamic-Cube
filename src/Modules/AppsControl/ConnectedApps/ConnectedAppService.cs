using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Modules.AppsControl.ConnectedApps;

/// <summary>The audience of connected apps' tokens on this installation.</summary>
public sealed record ConnectedAppSettings(string Audience);

/// <summary>
/// A connected app's sign-in as the Apps screen shows it: the client the app signs in with, where the identity provider
/// sends users back, and whether the identity provider holds the client as registered here.
/// </summary>
/// <param name="Managed">False when sign-in clients are maintained in the identity provider directly.</param>
/// <param name="InSync">The client exists, is enabled, has the connected-app audience, a service account and the app's return addresses.</param>
/// <param name="RegisteredRedirectUris">Return addresses the identity provider holds now.</param>
/// <param name="Issuer">Address the app signs in with (OpenID Connect discovery under it).</param>
/// <param name="ApiPath">Path of the connected-app API on the internal API's address.</param>
public sealed record ConnectedAppSignIn(
    string AppCode,
    string ClientId,
    bool Managed,
    bool Exists,
    bool Enabled,
    bool InSync,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> RegisteredRedirectUris,
    string? Issuer,
    string Audience,
    string ApiPath);

/// <summary>The result of setting up sign-in or replacing the secret: the secret is shown this once and never stored.</summary>
public sealed record ConnectedAppCredentials(ConnectedAppSignIn SignIn, string? Secret);

/// <summary>Sets up connected apps' sign-in clients and their secrets (ADR-0036 step 7).</summary>
public sealed partial class ConnectedAppService(
    AppsControlDbContext context,
    ISignInClients clients,
    ConnectedAppSettings settings,
    ICurrentUser currentUser,
    ILogger<ConnectedAppService> logger)
{
    public const string ApiPath = "/api/connect/v1";

    public async Task<ConnectedAppSignIn> GetAsync(string appCode, CancellationToken cancellationToken)
    {
        var app = await RequireAsync(appCode, cancellationToken);
        return await StatusAsync(app, cancellationToken);
    }

    /// <summary>Creates the app's sign-in client, or brings it in line with the app; a new client's secret is returned once.</summary>
    public async Task<ConnectedAppCredentials> SetUpAsync(string appCode, CancellationToken cancellationToken)
    {
        var app = await RequireAsync(appCode, cancellationToken);
        if (app.RedirectUris.Count == 0)
        {
            throw new AccountException("Give the app's address or its sign-in return addresses first (and publish the change).", AccountProblem.Invalid, "redirectUris");
        }

        var spec = new SignInClientSpec(app.ClientId!, $"{app.NameEn} ({app.Code})", app.RedirectUris, ConnectedClients.OriginsOf(app.RedirectUris), settings.Audience);
        var created = await clients.SaveAsync(spec, cancellationToken);
        var secret = created ? await clients.SecretAsync(spec.ClientId, cancellationToken) : null;
        LogSetUp(logger, spec.ClientId, app.Code, created ? "created" : "updated", currentUser.UserName ?? currentUser.UserId);
        return new ConnectedAppCredentials(await StatusAsync(app, cancellationToken), secret);
    }

    /// <summary>Replaces the app's secret: the app must use the new one from now on.</summary>
    public async Task<ConnectedAppCredentials> NewSecretAsync(string appCode, CancellationToken cancellationToken)
    {
        var app = await RequireAsync(appCode, cancellationToken);
        var secret = await clients.NewSecretAsync(app.ClientId!, cancellationToken);
        LogNewSecret(logger, app.ClientId!, app.Code, currentUser.UserName ?? currentUser.UserId);
        return new ConnectedAppCredentials(await StatusAsync(app, cancellationToken), secret);
    }

    private async Task<ConnectedAppSignIn> StatusAsync(BusinessApplication app, CancellationToken cancellationToken)
    {
        var clientId = app.ClientId!;
        if (!clients.IsManaged)
        {
            return new ConnectedAppSignIn(app.Code, clientId, false, false, false, false, app.RedirectUris, [], null, settings.Audience, ApiPath);
        }

        var client = await clients.FindAsync(clientId, settings.Audience, cancellationToken);
        var issuer = await clients.IssuerAsync(cancellationToken);
        var registered = client?.RedirectUris ?? [];
        var inSync = client is { Enabled: true, HasAudience: true, ServiceAccount: true }
            && registered.Order(StringComparer.Ordinal).SequenceEqual(app.RedirectUris.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        return new ConnectedAppSignIn(app.Code, clientId, true, client is not null, client?.Enabled ?? false, inSync, app.RedirectUris, registered, issuer, settings.Audience, ApiPath);
    }

    private async Task<BusinessApplication> RequireAsync(string appCode, CancellationToken cancellationToken)
    {
        var app = await context.BusinessApps.AsNoTracking().SingleOrDefaultAsync(a => a.Code == appCode, cancellationToken)
            ?? throw new AccountException($"There is no published app {appCode}.", AccountProblem.NotFound);
        if (app.Kind != AppKind.Connected || app.ClientId is null)
        {
            throw new AccountException($"App {appCode} is built on Dynamic Cube; only a connected app has a sign-in client of its own.", AccountProblem.Invalid, "kind");
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in client {ClientId} of connected app {App} {Action} by {UserName}")]
    private static partial void LogSetUp(ILogger logger, string clientId, string app, string action, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Secret of sign-in client {ClientId} (connected app {App}) replaced by {UserName}")]
    private static partial void LogNewSecret(ILogger logger, string clientId, string app, string userName);
}
