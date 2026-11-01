using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>An API client's sign-in client as it stands, and where partners get tokens and call the API.</summary>
/// <param name="Managed">False when clients are maintained in the identity provider directly.</param>
/// <param name="InSync">Enabled, a service account and issuing tokens with the public API's audience.</param>
public sealed record ApiClientSignIn(
    string Code,
    string ClientId,
    bool Managed,
    bool Exists,
    bool Enabled,
    bool InSync,
    string? TokenUrl,
    string Audience,
    string ApiUrl);

/// <summary>The result of setting up sign-in or replacing the secret: the secret is shown this once and never stored.</summary>
public sealed record ApiClientCredentials(ApiClientSignIn SignIn, string? Secret);

/// <summary>Sets up API clients' sign-in clients (client credentials only) and replaces their secrets (ADR-0042).</summary>
public sealed partial class ApiClientSignIns(
    IntegrationDbContext context,
    IServiceClients clients,
    PublicApiSettings settings,
    ICurrentUser currentUser,
    ILogger<ApiClientSignIns> logger)
{
    public async Task<ApiClientSignIn> GetAsync(string code, CancellationToken cancellationToken) =>
        await StatusAsync(await RequireAsync(code, cancellationToken), cancellationToken);

    /// <summary>Creates the client, or brings it in line; a new client's secret is returned once.</summary>
    public async Task<ApiClientCredentials> SetUpAsync(string code, CancellationToken cancellationToken)
    {
        var client = await RequireAsync(code, cancellationToken);
        Managed();
        var created = await clients.SaveAsync(client.ClientId, $"{client.NameEn} ({client.Code})", settings.Audience, cancellationToken);
        var secret = created ? await clients.SecretAsync(client.ClientId, cancellationToken) : null;
        LogSetUp(logger, client.ClientId, client.Code, created ? "created" : "updated", currentUser.UserName ?? currentUser.UserId);
        return new ApiClientCredentials(await StatusAsync(client, cancellationToken), secret);
    }

    /// <summary>Replaces the secret: the partner must use the new one from now on.</summary>
    public async Task<ApiClientCredentials> NewSecretAsync(string code, CancellationToken cancellationToken)
    {
        var client = await RequireAsync(code, cancellationToken);
        Managed();
        var secret = await clients.NewSecretAsync(client.ClientId, cancellationToken);
        LogNewSecret(logger, client.ClientId, client.Code, currentUser.UserName ?? currentUser.UserId);
        return new ApiClientCredentials(await StatusAsync(client, cancellationToken), secret);
    }

    /// <summary>The token address partners use, when the identity provider can tell.</summary>
    public Task<string?> TokenUrlAsync(CancellationToken cancellationToken) => clients.TokenUrlAsync(cancellationToken);

    public string ApiUrl => (settings.BaseUrl?.TrimEnd('/') ?? string.Empty) + PublicApiSettings.Path;

    private void Managed()
    {
        if (!clients.IsManaged)
        {
            throw ChangeValidationException.For("clientId", "Sign-in clients are maintained in the identity provider directly: create the client there with client credentials and the public API's audience.");
        }
    }

    private async Task<ApiClientSignIn> StatusAsync(ApiClient client, CancellationToken cancellationToken)
    {
        var tokenUrl = await clients.TokenUrlAsync(cancellationToken);
        if (!clients.IsManaged)
        {
            return new ApiClientSignIn(client.Code, client.ClientId, false, false, false, false, tokenUrl, settings.Audience, ApiUrl);
        }

        var found = await clients.FindAsync(client.ClientId, settings.Audience, cancellationToken);
        var inSync = found is { Enabled: true, ServiceAccount: true, HasAudience: true };
        return new ApiClientSignIn(client.Code, client.ClientId, true, found is not null, found?.Enabled ?? false, inSync, tokenUrl, settings.Audience, ApiUrl);
    }

    private async Task<ApiClient> RequireAsync(string code, CancellationToken cancellationToken) =>
        await context.ApiClients.AsNoTracking().SingleOrDefaultAsync(c => c.Code == code, cancellationToken)
            ?? throw new ApiClientNotFoundException($"There is no published API client {code}.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in client {ClientId} of API client {Client} {Action} by {UserName}")]
    private static partial void LogSetUp(ILogger logger, string clientId, string client, string action, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Secret of sign-in client {ClientId} (API client {Client}) replaced by {UserName}")]
    private static partial void LogNewSecret(ILogger logger, string clientId, string client, string userName);
}

public sealed class ApiClientNotFoundException(string message) : Exception(message);
