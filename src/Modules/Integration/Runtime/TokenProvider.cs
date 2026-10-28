using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Security;
using Microsoft.Extensions.Caching.Memory;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>Credentials could not be read or a token could not be obtained.</summary>
internal sealed class IntegrationAuthException(string message) : Exception(message);

/// <summary>Adds the connection's credentials to requests; OAuth2 tokens are cached until shortly before they expire.</summary>
internal sealed class TokenProvider(IHttpClientFactory httpClients, IMemoryCache cache, ISecretStore secrets, DefinitionCache definitions)
{
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    public async Task AuthorizeAsync(HttpRequestMessage request, Connection connection, CancellationToken cancellationToken)
    {
        switch (connection.AuthType)
        {
            case AuthType.Basic:
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicValue(Secret(connection, "username"), Secret(connection, "password")));
                break;
            case AuthType.ApiKey:
                request.Headers.TryAddWithoutValidation(connection.ApiKeyHeader ?? "X-API-Key", Secret(connection, "apiKey"));
                break;
            case AuthType.OAuth2ClientCredentials:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(connection, cancellationToken));
                break;
        }
    }

    /// <summary>Drops a cached token the provider rejected.</summary>
    public void Forget(Connection connection) => cache.Remove(Key(connection));

    private async Task<string> TokenAsync(Connection connection, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key(connection), out string? token) && token is not null)
        {
            return token;
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(Key(connection), out token) && token is not null)
            {
                return token;
            }

            var (value, expiresIn) = await RequestTokenAsync(connection, cancellationToken);
            cache.Set(Key(connection), value, TimeSpan.FromSeconds(Math.Max(30, expiresIn - 60)));
            return value;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private async Task<(string Token, int ExpiresIn)> RequestTokenAsync(Connection connection, CancellationToken cancellationToken)
    {
        // "/oauth/v1/token" parses as an absolute file URI on Unix, so only http(s) URLs count as absolute.
        var url = Uri.TryCreate(connection.TokenUrl, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp)
            ? absolute
            : new Uri(connection.BaseUrl + "/" + connection.TokenUrl!.TrimStart('/'));
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")]),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicValue(Secret(connection, "clientId"), Secret(connection, "clientSecret")));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(connection.TimeoutSeconds));
        HttpResponseMessage response;
        try
        {
            response = await httpClients.CreateClient(IntegrationGateway.HttpClientName).SendAsync(request, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new IntegrationAuthException($"Token request failed: {ex.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new IntegrationAuthException($"Token request returned HTTP {(int)response.StatusCode}.");
            }

            JsonNode? json;
            try
            {
                json = JsonNode.Parse(body);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new IntegrationAuthException("Token response is not JSON.");
            }

            var token = JsonPaths.ReadText(json, "access_token") ?? throw new IntegrationAuthException("Token response has no access_token.");
            var expiresIn = int.TryParse(JsonPaths.ReadText(json, "expires_in"), System.Globalization.CultureInfo.InvariantCulture, out var seconds) ? seconds : 300;
            return (token, expiresIn);
        }
    }

    private string Key(Connection connection) => $"integ:token:{definitions.Version}:{connection.Code}";

    private string Secret(Connection connection, string key) =>
        secrets.Find(connection.SecretName ?? string.Empty, key)
        ?? throw new IntegrationAuthException($"Secret '{connection.SecretName}' has no '{key}' (configure Secrets:{connection.SecretName}:{key}).");

    private static string BasicValue(string user, string password) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
}
