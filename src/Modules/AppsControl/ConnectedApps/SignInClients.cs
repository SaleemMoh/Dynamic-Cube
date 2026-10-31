using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Platform.Security;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.AppsControl.ConnectedApps;

/// <summary>What a connected app's sign-in client should be (ADR-0036 step 7).</summary>
/// <param name="Audience">Added to the tokens the client obtains, so the platform's connected-app API accepts them.</param>
public sealed record SignInClientSpec(string ClientId, string Name, IReadOnlyList<string> RedirectUris, IReadOnlyList<string> WebOrigins, string Audience);

/// <summary>A sign-in client as the identity provider holds it.</summary>
public sealed record SignInClient(string ClientId, bool Enabled, IReadOnlyList<string> RedirectUris, IReadOnlyList<string> WebOrigins, bool ServiceAccount, bool HasAudience);

/// <summary>Connected apps' sign-in clients in the identity provider: created, updated and given new secrets from Apps Control.</summary>
public interface ISignInClients
{
    /// <summary>False when sign-in clients are maintained in the identity provider directly (Provider <c>None</c>).</summary>
    bool IsManaged { get; }

    /// <summary>The issuer the app signs in with (the realm's address as browsers see it), when it can be read.</summary>
    Task<string?> IssuerAsync(CancellationToken cancellationToken);

    Task<SignInClient?> FindAsync(string clientId, string audience, CancellationToken cancellationToken);

    /// <summary>Creates the client or brings it in line with the spec; true when it was created.</summary>
    Task<bool> SaveAsync(SignInClientSpec spec, CancellationToken cancellationToken);

    /// <summary>The client's current secret (shown once, right after the client is created).</summary>
    Task<string> SecretAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Replaces the client's secret; the old one stops working at once.</summary>
    Task<string> NewSecretAsync(string clientId, CancellationToken cancellationToken);
}

/// <summary>
/// Sign-in clients in a Keycloak realm, through its admin REST API with the account service account (which needs
/// realm-management <c>manage-clients</c> and <c>view-clients</c> besides the user roles). The secret is Keycloak's:
/// the platform never stores it.
/// </summary>
internal sealed class KeycloakSignInClients(IHttpClientFactory httpClients, IOptions<IdentityOptions> options, ISecretStore secrets, TimeProvider clock)
    : ISignInClients
{
    private const string AudienceMapper = "oidc-audience-mapper";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly Dictionary<string, (string Token, DateTimeOffset Expires)> Tokens = new(StringComparer.Ordinal);

    private IdentityOptions Options => options.Value;

    public bool IsManaged => true;

    public async Task<string?> IssuerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClients.CreateClient(KeycloakAccountDirectory.HttpClientName);
            var discovery = await client.GetFromJsonAsync<JsonElement>(new Uri($"{BaseUrl}/realms/{Uri.EscapeDataString(Options.Realm)}/.well-known/openid-configuration"), cancellationToken);
            return discovery.TryGetProperty("issuer", out var issuer) ? issuer.GetString() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<SignInClient?> FindAsync(string clientId, string audience, CancellationToken cancellationToken)
    {
        var rep = await FindRepAsync(clientId, cancellationToken);
        return rep is null ? null : new SignInClient(
            rep.ClientId ?? clientId,
            rep.Enabled ?? false,
            rep.RedirectUris ?? [],
            rep.WebOrigins ?? [],
            rep.ServiceAccountsEnabled ?? false,
            HasAudience(rep, audience));
    }

    public async Task<bool> SaveAsync(SignInClientSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var existing = await FindRepAsync(spec.ClientId, cancellationToken);
        if (existing is null)
        {
            var rep = new ClientRep
            {
                ClientId = spec.ClientId,
                Name = spec.Name,
                Description = "Connected app registered in Dynamic Cube Apps Control.",
                Enabled = true,
                Protocol = "openid-connect",
                PublicClient = false,
                ClientAuthenticatorType = "client-secret",
                StandardFlowEnabled = true,
                DirectAccessGrantsEnabled = false,
                ImplicitFlowEnabled = false,
                ServiceAccountsEnabled = true,
                RedirectUris = [.. spec.RedirectUris],
                WebOrigins = [.. spec.WebOrigins],
                Attributes = Attributes(null, spec),
                ProtocolMappers = [Mapper(spec.Audience)],
            };
            using var created = await SendAsync(HttpMethod.Post, "clients", rep, cancellationToken);
            await EnsureAsync(created, cancellationToken);
            return true;
        }

        existing.Name = spec.Name;
        existing.Enabled = true;
        existing.PublicClient = false;
        existing.StandardFlowEnabled = true;
        existing.ServiceAccountsEnabled = true;
        existing.RedirectUris = [.. spec.RedirectUris];
        existing.WebOrigins = [.. spec.WebOrigins];
        existing.Attributes = Attributes(existing.Attributes, spec);
        var addMapper = !HasAudience(existing, spec.Audience);
        existing.ProtocolMappers = null;
        using (var updated = await SendAsync(HttpMethod.Put, $"clients/{existing.Id}", existing, cancellationToken))
        {
            await EnsureAsync(updated, cancellationToken);
        }

        if (addMapper)
        {
            using var mapper = await SendAsync(HttpMethod.Post, $"clients/{existing.Id}/protocol-mappers/models", Mapper(spec.Audience), cancellationToken);
            await EnsureAsync(mapper, cancellationToken);
        }

        return false;
    }

    public async Task<string> SecretAsync(string clientId, CancellationToken cancellationToken) =>
        await SecretAsync(clientId, HttpMethod.Get, cancellationToken);

    public async Task<string> NewSecretAsync(string clientId, CancellationToken cancellationToken) =>
        await SecretAsync(clientId, HttpMethod.Post, cancellationToken);

    private async Task<string> SecretAsync(string clientId, HttpMethod method, CancellationToken cancellationToken)
    {
        var rep = await FindRepAsync(clientId, cancellationToken)
            ?? throw new AccountException($"There is no sign-in client {clientId}; set up the app's sign-in first.", AccountProblem.NotFound);
        using var response = await SendAsync(method, $"clients/{rep.Id}/client-secret", null, cancellationToken);
        await EnsureAsync(response, cancellationToken);
        var secret = await response.Content.ReadFromJsonAsync<SecretRep>(Json, cancellationToken);
        return secret?.Value ?? throw new AccountException("The identity provider returned no secret.", AccountProblem.Unavailable);
    }

    private static Dictionary<string, string> Attributes(Dictionary<string, string>? current, SignInClientSpec spec)
    {
        var attributes = current is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(current, StringComparer.Ordinal);
        attributes["post.logout.redirect.uris"] = string.Join("##", spec.RedirectUris);
        attributes["pkce.code.challenge.method"] = "S256";
        return attributes;
    }

    private static MapperRep Mapper(string audience) => new()
    {
        Name = "audience-" + audience,
        Protocol = "openid-connect",
        ProtocolMapper = AudienceMapper,
        Config = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["included.custom.audience"] = audience,
            ["access.token.claim"] = "true",
            ["id.token.claim"] = "false",
            ["introspection.token.claim"] = "true",
        },
    };

    private static bool HasAudience(ClientRep rep, string audience) =>
        rep.ProtocolMappers?.Any(m => m.ProtocolMapper == AudienceMapper
            && m.Config is { } config
            && (config.GetValueOrDefault("included.custom.audience") == audience || config.GetValueOrDefault("included.client.audience") == audience)) == true;

    private async Task<ClientRep?> FindRepAsync(string clientId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"clients?clientId={Uri.EscapeDataString(clientId)}", null, cancellationToken);
        await EnsureAsync(response, cancellationToken);
        var clients = await response.Content.ReadFromJsonAsync<List<ClientRep>>(Json, cancellationToken) ?? [];
        return clients.FirstOrDefault(c => c.ClientId == clientId);
    }

    private string BaseUrl => (Options.BaseUrl ?? throw new AccountException($"The Keycloak address (BaseUrl) for the realm {Options.Realm} is not set.", AccountProblem.Unavailable)).TrimEnd('/');

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var client = httpClients.CreateClient(KeycloakAccountDirectory.HttpClientName);
        using var request = new HttpRequestMessage(method, $"{BaseUrl}/admin/realms/{Uri.EscapeDataString(Options.Realm)}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(client, cancellationToken));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        try
        {
            return await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AccountException($"The identity provider cannot be reached: {ex.Message}", AccountProblem.Unavailable);
        }
    }

    private async Task<string> TokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            var key = $"{BaseUrl}|{Options.Realm}|{Options.ClientId}";
            if (Tokens.TryGetValue(key, out var cached) && cached.Expires > clock.GetUtcNow().AddSeconds(30))
            {
                return cached.Token;
            }

            var secret = secrets.Find(Options.SecretName, "clientSecret")
                ?? throw new AccountException($"The secret {Options.SecretName}:clientSecret is not set.", AccountProblem.Unavailable);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = Options.ClientId,
                ["client_secret"] = secret,
            });
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(new Uri($"{BaseUrl}/realms/{Uri.EscapeDataString(Options.Realm)}/protocol/openid-connect/token"), form, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new AccountException($"The identity provider cannot be reached: {ex.Message}", AccountProblem.Unavailable);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new AccountException($"The identity provider refused the service account ({(int)response.StatusCode}).", AccountProblem.Unavailable);
                }

                var token = await response.Content.ReadFromJsonAsync<TokenRep>(Json, cancellationToken)
                    ?? throw new AccountException("The identity provider returned no token.", AccountProblem.Unavailable);
                Tokens[key] = (token.AccessToken, clock.GetUtcNow().AddSeconds(token.ExpiresIn));
                return token.AccessToken;
            }
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new AccountException(
                "The identity provider refused: its service account may not manage sign-in clients (give it realm-management manage-clients and view-clients).",
                AccountProblem.Unavailable);
        }

        string? message = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorRep>(Json, cancellationToken);
            message = error?.ErrorMessage ?? error?.Error;
        }
        catch (JsonException)
        {
        }

        message ??= $"The identity provider answered {(int)response.StatusCode}.";
        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new AccountException(message, AccountProblem.NotFound),
            HttpStatusCode.Conflict => new AccountException(message, AccountProblem.Conflict, "clientId"),
            HttpStatusCode.BadRequest => new AccountException(message, AccountProblem.Invalid),
            _ => new AccountException(message, AccountProblem.Unavailable),
        };
    }

    private sealed record TokenRep([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record ErrorRep(string? Error, string? ErrorMessage);

    private sealed record SecretRep(string? Type, string? Value);

    /// <summary>Keycloak's client representation (only the properties used here are read and written back).</summary>
    private sealed class ClientRep
    {
        public string? Id { get; set; }

        public string? ClientId { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public bool? Enabled { get; set; }

        public string? Protocol { get; set; }

        public bool? PublicClient { get; set; }

        public string? ClientAuthenticatorType { get; set; }

        public bool? StandardFlowEnabled { get; set; }

        public bool? DirectAccessGrantsEnabled { get; set; }

        public bool? ImplicitFlowEnabled { get; set; }

        public bool? ServiceAccountsEnabled { get; set; }

        public List<string>? RedirectUris { get; set; }

        public List<string>? WebOrigins { get; set; }

        public Dictionary<string, string>? Attributes { get; set; }

        public List<MapperRep>? ProtocolMappers { get; set; }
    }

    private sealed class MapperRep
    {
        public string? Name { get; set; }

        public string? Protocol { get; set; }

        public string? ProtocolMapper { get; set; }

        public Dictionary<string, string>? Config { get; set; }
    }
}

/// <summary>Sign-in clients are maintained in the identity provider directly (Provider <c>None</c>).</summary>
internal sealed class UnmanagedSignInClients : ISignInClients
{
    public bool IsManaged => false;

    public Task<string?> IssuerAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    public Task<SignInClient?> FindAsync(string clientId, string audience, CancellationToken cancellationToken) => Task.FromResult<SignInClient?>(null);

    public Task<bool> SaveAsync(SignInClientSpec spec, CancellationToken cancellationToken) => throw Unavailable();

    public Task<string> SecretAsync(string clientId, CancellationToken cancellationToken) => throw Unavailable();

    public Task<string> NewSecretAsync(string clientId, CancellationToken cancellationToken) => throw Unavailable();

    private static AccountException Unavailable() =>
        new("Sign-in clients are maintained in the identity provider, not in Apps Control (its Provider setting is None).", AccountProblem.Unavailable);
}
