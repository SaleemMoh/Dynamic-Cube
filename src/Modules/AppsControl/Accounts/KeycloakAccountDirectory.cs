using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicCube.Platform.Security;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.AppsControl.Accounts;

/// <summary>
/// Accounts in a Keycloak realm (employees', or applicants' portal accounts), through its admin REST API with a
/// service-account token. Accounts linked to a
/// user federation (LDAP, Active Directory) or to an identity provider (Entra ID, Nafath…) belong to that system:
/// their passwords cannot be set here, but they can still be enabled, disabled and signed out.
/// </summary>
internal sealed class KeycloakAccountDirectory(IHttpClientFactory httpClients, IOptions<IdentityOptions> options, ISecretStore secrets, TimeProvider clock)
    : IAccountDirectory
{
    public const string HttpClientName = "dc.identity";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly string[] UpdatePassword = ["UPDATE_PASSWORD"];
    private static readonly Dictionary<string, (string Token, DateTimeOffset Expires)> Tokens = new(StringComparer.Ordinal);

    private IdentityOptions Options => options.Value;

    public async Task<AccountCapabilities> CapabilitiesAsync(CancellationToken cancellationToken)
    {
        // What is connected is shown for information; a service account without the rights to read it still works.
        var federations = new List<string>();
        foreach (var component in await OptionalAsync<List<ComponentRep>>("components?type=org.keycloak.storage.UserStorageProvider", cancellationToken) ?? [])
        {
            federations.Add($"{component.ProviderId?.ToUpperInvariant() ?? "LDAP"}: {component.Name}");
        }

        foreach (var provider in await OptionalAsync<List<IdentityProviderRep>>("identity-provider/instances", cancellationToken) ?? [])
        {
            if (provider.Enabled)
            {
                federations.Add(provider.DisplayName ?? provider.Alias ?? "?");
            }
        }

        var managed = !Options.IsFederated;
        return new AccountCapabilities("Keycloak", managed ? "Managed" : "Federated", managed, managed, true, federations);
    }

    public async Task<AccountView> GetAsync(string userName, CancellationToken cancellationToken)
    {
        var user = await FindAsync(userName, cancellationToken);
        if (user is null)
        {
            return new AccountView(userName, false, false, "LOCAL", false, null, false, [], null);
        }

        var source = await SourceAsync(user, cancellationToken);
        return View(user, source);
    }

    public async Task<AccountPage> SearchAsync(string? search, int first, int max, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(search) ? string.Empty : $"search={Uri.EscapeDataString(search.Trim())}&";
        var users = await GetAsync<List<UserRep>>($"users?{query}first={Math.Max(0, first)}&max={Math.Clamp(max, 1, 100)}&briefRepresentation=false", cancellationToken) ?? [];
        var total = await GetAsync<int?>($"users/count{(query.Length == 0 ? string.Empty : "?" + query.TrimEnd('&'))}", cancellationToken) ?? users.Count;
        var items = new List<AccountView>(users.Count);
        foreach (var user in users.Where(u => u.ServiceAccountClientId is null))
        {
            items.Add(View(user, await SourceAsync(user, cancellationToken)));
        }

        return new AccountPage(items, total);
    }

    public async Task<AccountView> CreateAsync(NewAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Options.IsFederated)
        {
            throw new AccountException("Accounts come from the directory or identity provider; create the user there.", AccountProblem.Unavailable);
        }

        var userName = request.UserName.Trim().ToLowerInvariant();
        if (await FindAsync(userName, cancellationToken) is not null)
        {
            throw new AccountException($"An account '{userName}' already exists.", AccountProblem.Conflict, "userName");
        }

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) && !request.SendSetupEmail)
        {
            throw new AccountException("Give a temporary password or send the setup email.", AccountProblem.Invalid, "temporaryPassword");
        }

        if (request.SendSetupEmail && string.IsNullOrWhiteSpace(request.Email))
        {
            throw new AccountException("The setup email needs an email address.", AccountProblem.Invalid, "email");
        }

        var create = new UserRep
        {
            Username = userName,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Enabled = true,
            RequiredActions = ["UPDATE_PASSWORD"],
            Attributes = request.Attributes?.ToDictionary(a => a.Key, a => new List<string> { a.Value }),
        };
        using (var response = await SendAsync(HttpMethod.Post, "users", create, cancellationToken))
        {
            await EnsureAsync(response, "userName", cancellationToken);
        }

        var user = await FindAsync(userName, cancellationToken) ?? throw new AccountException("The account was not created.", AccountProblem.Unavailable);
        await SetPasswordAsync(user, new PasswordRequest(request.TemporaryPassword, request.SendSetupEmail), cancellationToken);
        return View((await FindAsync(userName, cancellationToken))!, "LOCAL");
    }

    public async Task SetEnabledAsync(string userName, bool enabled, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userName, cancellationToken);
        using var response = await SendAsync(HttpMethod.Put, $"users/{user.Id}", new UserRep { Enabled = enabled }, cancellationToken);
        await EnsureAsync(response, null, cancellationToken);
        if (!enabled)
        {
            await SignOutAsync(user, cancellationToken);
        }
    }

    public async Task SetPasswordAsync(string userName, PasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await RequireAsync(userName, cancellationToken);
        var source = await SourceAsync(user, cancellationToken);
        if (Options.IsFederated || source != "LOCAL")
        {
            throw new AccountException($"The password of this account is managed by {(source == "LOCAL" ? "the directory" : source)}.", AccountProblem.Unavailable);
        }

        await SetPasswordAsync(user, request, cancellationToken);
    }

    public async Task SignOutAsync(string userName, CancellationToken cancellationToken) =>
        await SignOutAsync(await RequireAsync(userName, cancellationToken), cancellationToken);

    private async Task SetPasswordAsync(UserRep user, PasswordRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            var credential = new { type = "password", value = request.TemporaryPassword, temporary = true };
            using var response = await SendAsync(HttpMethod.Put, $"users/{user.Id}/reset-password", credential, cancellationToken);
            await EnsureAsync(response, "temporaryPassword", cancellationToken);
        }

        if (request.SendEmail)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                throw new AccountException("The account has no email address to send the link to.", AccountProblem.Invalid, "email");
            }

            using var response = await SendAsync(HttpMethod.Put, $"users/{user.Id}/execute-actions-email?lifespan=86400", UpdatePassword, cancellationToken);
            await EnsureAsync(response, "email", cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            throw new AccountException("Give a temporary password or send the email.", AccountProblem.Invalid, "temporaryPassword");
        }
    }

    private async Task SignOutAsync(UserRep user, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"users/{user.Id}/logout", null, cancellationToken);
        await EnsureAsync(response, null, cancellationToken);
    }

    private async Task<string> SourceAsync(UserRep user, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(user.FederationLink))
        {
            var component = await GetAsync<ComponentRep>($"components/{user.FederationLink}", cancellationToken);
            return component?.Name ?? "LDAP";
        }

        var identities = await GetAsync<List<FederatedIdentityRep>>($"users/{user.Id}/federated-identity", cancellationToken) ?? [];
        return identities.Count > 0 ? identities[0].IdentityProvider ?? "IDP" : "LOCAL";
    }

    private AccountView View(UserRep user, string source) => new(
        user.Username ?? string.Empty,
        true,
        user.Enabled ?? false,
        source,
        source == "LOCAL" && !Options.IsFederated,
        user.Email,
        user.EmailVerified ?? false,
        user.RequiredActions ?? [],
        user.CreatedTimestamp is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null,
        user.FirstName,
        user.LastName,
        user.Attributes?.Where(a => a.Value.Count > 0).ToDictionary(a => a.Key, a => a.Value[0]));

    private async Task<UserRep> RequireAsync(string userName, CancellationToken cancellationToken) =>
        await FindAsync(userName, cancellationToken) ?? throw new AccountException($"There is no sign-in account '{userName}'.", AccountProblem.NotFound);

    private async Task<UserRep?> FindAsync(string userName, CancellationToken cancellationToken)
    {
        var users = await GetAsync<List<UserRep>>($"users?username={Uri.EscapeDataString(userName.Trim())}&exact=true&briefRepresentation=false", cancellationToken) ?? [];
        return users.FirstOrDefault(u => string.Equals(u.Username, userName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<T?> OptionalAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await GetAsync<T>(path, cancellationToken);
        }
        catch (AccountException ex) when (ex.Problem == AccountProblem.Unavailable && ex.Message.Contains("403", StringComparison.Ordinal))
        {
            return default;
        }
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureAsync(response, null, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var client = httpClients.CreateClient(HttpClientName);
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

    private string BaseUrl => (Options.BaseUrl ?? throw new AccountException($"The Keycloak address (BaseUrl) for the realm {Options.Realm} is not set.", AccountProblem.Unavailable)).TrimEnd('/');

    private async Task<string> TokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            // One token per Keycloak, realm and client, reused until shortly before it expires.
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

                var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken)
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

    /// <summary>Turns Keycloak's answer into an <see cref="AccountException"/> (e.g. a password the policy refuses).</summary>
    private static async Task EnsureAsync(HttpResponseMessage response, string? field, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
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
            HttpStatusCode.Conflict => new AccountException(message, AccountProblem.Conflict, field),
            HttpStatusCode.BadRequest => new AccountException(message, AccountProblem.Invalid, field),
            _ => new AccountException(message, AccountProblem.Unavailable),
        };
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record ErrorRep(string? Error, string? ErrorMessage);

    private sealed class UserRep
    {
        public string? Id { get; init; }

        public string? Username { get; init; }

        public string? Email { get; init; }

        public bool? EmailVerified { get; init; }

        public string? FirstName { get; init; }

        public string? LastName { get; init; }

        public bool? Enabled { get; init; }

        public string? FederationLink { get; init; }

        public List<string>? RequiredActions { get; init; }

        public long? CreatedTimestamp { get; init; }

        public Dictionary<string, List<string>>? Attributes { get; init; }

        public string? ServiceAccountClientId { get; init; }
    }

    private sealed record ComponentRep(string? Id, string? Name, string? ProviderId);

    private sealed record IdentityProviderRep(string? Alias, string? DisplayName, bool Enabled);

    private sealed record FederatedIdentityRep(string? IdentityProvider, string? UserName);
}
