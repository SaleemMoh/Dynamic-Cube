using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DynamicCube.Platform.Nafath;
using DynamicCube.Platform.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DynamicCube.Modules.Integration.Nafath;

/// <summary>Nafath's web sign-in session: the page to send the person to, and the state that redeems the answer.</summary>
public sealed record NafathWebSession(string Url, string State, string HashedState);

/// <summary>
/// Elm's Nafath API through Rabet (ADR-0043). Web sign-in: <c>GET /api/v2/oidc/session</c> gives Nafath's page (with the
/// state in its query), the person is redirected there and back, and <c>POST /api/v2/oidc/jwt</c> redeems the state for
/// the signed answer. Verification in the app: <c>POST /api/v1/mfa/request</c> gives the number to show,
/// <c>POST /api/v1/mfa/request/status</c> tells whether it was answered, and the signed answer is posted to the callback.
/// Answers are verified with Nafath's keys (<c>GET /api/v1/mfa/jwk</c>). Every call carries <c>APP-ID</c>, <c>APP-KEY</c>
/// and <c>X-Forwarded-For</c> (the person, then this server).
/// </summary>
public sealed partial class NafathClient(
    IHttpClientFactory httpClients,
    IOptions<NafathOptions> options,
    ISecretStore secrets,
    IMemoryCache cache,
    ILogger<NafathClient> logger)
{
    public const string HttpClientName = "dynamic-cube-nafath";

    /// <summary>Nafath's code while the person has not answered yet.</summary>
    private const string Pending = "401-033";

    private NafathOptions Options => options.Value;

    public async Task<NafathWebSession> CreateWebSessionAsync(string requestId, string? userIp, CancellationToken cancellationToken)
    {
        var body = await SendAsync(HttpMethod.Get, $"/api/v2/oidc/session?locale={Uri.EscapeDataString(Options.Locale)}&requestId={Uri.EscapeDataString(requestId)}", null, userIp, false, cancellationToken)
            ?? throw new NafathException("Nafath returned no sign-in session.");
        var url = body["url"]?.GetValue<string>();
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new NafathException("Nafath returned a session without its page; the service provider profile may not be linked to these credentials.");
        }

        var state = QueryValue(uri.Query, "state");
        if (string.IsNullOrEmpty(state))
        {
            throw new NafathException("Nafath's page carries no state.");
        }

        var hashed = body["hashedState"]?.GetValue<string>();
        return new NafathWebSession(url, state, string.IsNullOrEmpty(hashed) ? Digest(state) : hashed);
    }

    /// <summary>The signed answer of a web sign-in, or null while the person has not finished.</summary>
    public async Task<string?> RedeemWebTokenAsync(string state, string? userIp, CancellationToken cancellationToken)
    {
        var body = await SendAsync(HttpMethod.Post, "/api/v2/oidc/jwt", new JsonObject { ["state"] = state }, userIp, true, cancellationToken);
        if (body is null)
        {
            return null;
        }

        // The field is "token" (some integrations wrongly expect "jwt").
        return (body["token"] ?? body["jwt"] ?? body["id_token"])?.GetValue<string>() is { Length: > 0 } token
            ? token
            : throw new NafathException("Nafath answered without the signed answer.");
    }

    public async Task<(string TransId, string Random)> CreateMfaRequestAsync(string nationalId, string service, string requestId, string? userIp, CancellationToken cancellationToken)
    {
        var body = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/mfa/request?local={Uri.EscapeDataString(Options.Locale)}&requestId={Uri.EscapeDataString(requestId)}",
            new JsonObject { ["nationalId"] = nationalId, ["service"] = service },
            userIp,
            false,
            cancellationToken) ?? throw new NafathException("Nafath returned no request.");
        var transId = body["transId"]?.ToString();
        var random = body["random"]?.ToString();
        return string.IsNullOrEmpty(transId) || string.IsNullOrEmpty(random)
            ? throw new NafathException("Nafath's request has no transaction or number.")
            : (transId, random);
    }

    /// <summary><c>WAITING</c>, <c>COMPLETED</c> (approved; the signed answer comes to the callback), <c>REJECTED</c> or <c>EXPIRED</c>.</summary>
    public async Task<string> MfaStatusAsync(string nationalId, string transId, string random, string? userIp, CancellationToken cancellationToken)
    {
        var body = await SendAsync(
            HttpMethod.Post,
            "/api/v1/mfa/request/status",
            new JsonObject { ["nationalId"] = nationalId, ["transId"] = transId, ["random"] = random },
            userIp,
            false,
            cancellationToken);
        return (body?["status"]?.GetValue<string>() ?? "WAITING").ToUpperInvariant();
    }

    /// <summary>Checks a signed answer with Nafath's keys (fetched again once when the key is unknown) and reads the person.</summary>
    public async Task<(NafathPerson Person, string? TransId)> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, Parameters(await KeysAsync(false, cancellationToken)));
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            result = await handler.ValidateTokenAsync(token, Parameters(await KeysAsync(true, cancellationToken)));
        }

        if (!result.IsValid)
        {
            LogInvalidToken(logger, result.Exception?.GetType().Name ?? "invalid");
            throw new NafathException("Nafath's answer is not signed by Nafath.");
        }

        var claims = result.Claims;
        string? Text(params string[] names) => names.Select(n => claims.TryGetValue(n, out var v) ? Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() : null)
            .FirstOrDefault(v => !string.IsNullOrEmpty(v));
        string? Joined(params string[] names)
        {
            var parts = names.Select(n => Text(n)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            return parts.Count == 0 ? null : string.Join(' ', parts);
        }

        var identifier = Text("nin", "iqamaNumber", "visaNumber", "borderNumber")
            ?? throw new NafathException("Nafath's answer names no person.");
        var person = new NafathPerson(
            identifier,
            Joined("firstName", "fatherName", "secondName", "grandFatherName", "thirdName", "familyName", "lastName"),
            Joined("englishFirstName", "englishSecondName", "englishThirdName", "englishLastName")
                ?? Joined("translatedFirstName", "translatedSecondName", "translatedThirdName", "translatedLastName"),
            Text("nationality", "nationalityCode"),
            Text("dateOfBirthG", "birthgDate"),
            Text("gender"),
            Text("idExpiryDateG", "iqamaExpiryDateG", "visaExpiryDate"));
        return (person, Text("transId"));
    }

    /// <summary>Base64 SHA-256: how Nafath's state is matched when the person comes back.</summary>
    public static string Digest(string state) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(state)));

    private static TokenValidationParameters Parameters(IEnumerable<SecurityKey> keys) => new()
    {
        IssuerSigningKeys = keys,
        ValidateIssuer = false,
        ValidateAudience = false,
        RequireExpirationTime = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        RequireSignedTokens = true,
    };

    private async Task<IReadOnlyList<SecurityKey>> KeysAsync(bool refresh, CancellationToken cancellationToken)
    {
        var key = "nafath-jwk:" + Options.BaseUrl;
        if (!refresh && cache.TryGetValue(key, out IReadOnlyList<SecurityKey>? keys) && keys is { Count: > 0 })
        {
            return keys;
        }

        var body = await SendAsync(HttpMethod.Get, "/api/v1/mfa/jwk", null, null, false, cancellationToken)
            ?? throw new NafathException("Nafath returned no keys.");
        var set = new JsonWebKeySet(body.ToJsonString());
        keys = [.. set.GetSigningKeys()];
        if (keys.Count == 0)
        {
            throw new NafathException("Nafath returned no signing keys.");
        }

        cache.Set(key, keys, TimeSpan.FromHours(12));
        return keys;
    }

    private async Task<JsonObject?> SendAsync(HttpMethod method, string path, JsonObject? json, string? userIp, bool allowPending, CancellationToken cancellationToken)
    {
        if (!Options.IsConfigured)
        {
            throw new NafathException("Nafath is not set up on this installation (Nafath:BaseUrl).");
        }

        var appId = secrets.Find(Options.SecretName, "appId");
        var appKey = secrets.Find(Options.SecretName, "appKey");
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appKey))
        {
            throw new NafathException($"The Nafath credentials Secrets:{Options.SecretName}:appId and appKey are not set.");
        }

        using var request = new HttpRequestMessage(method, Options.BaseUrl!.TrimEnd('/') + path);
        request.Headers.Add("APP-ID", appId);
        request.Headers.Add("APP-KEY", appKey);
        request.Headers.Add("Accept", "application/json");
        var forwarded = string.Join(", ", new[] { userIp, Options.ServerIp }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (forwarded.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwarded);
        }

        if (json is not null)
        {
            request.Content = JsonContent.Create(json);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new NafathException("Nafath cannot be reached: " + ex.Message, ex);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonObject? body = null;
            try
            {
                body = text.Length == 0 ? null : JsonNode.Parse(text) as JsonObject;
            }
            catch (System.Text.Json.JsonException)
            {
                body = null;
            }

            if (response.IsSuccessStatusCode)
            {
                return body ?? [];
            }

            var code = body?["code"]?.ToString();
            if (allowPending && response.StatusCode == HttpStatusCode.Unauthorized && code?.StartsWith(Pending, StringComparison.Ordinal) == true)
            {
                return null;
            }

            LogRefused(logger, (int)response.StatusCode, code ?? "-", body?["reference"]?.ToString() ?? "-");
            throw response.StatusCode == HttpStatusCode.Forbidden
                ? new NafathException("Nafath refused these credentials for this environment (APP-ID/APP-KEY).", code)
                : code switch
                {
                    "400-034-050" => new NafathException("A Nafath request for this person is already waiting: answer it in the app or wait for it to end.", code),
                    "400-034-051" => new NafathException("The Nafath request has ended.", code),
                    "400-034-053" => new NafathException("Nafath does not know this request.", code),
                    _ => new NafathException($"Nafath answered {(int)response.StatusCode}{(code is null ? string.Empty : " " + code)}.", code),
                };
        }
    }

    private static string? QueryValue(string query, string name)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = part.IndexOf('=', StringComparison.Ordinal);
            var key = Uri.UnescapeDataString(index < 0 ? part : part[..index]);
            if (string.Equals(key, name, StringComparison.Ordinal))
            {
                // The state is base64: a literal + stays a + (it is not a space here).
                return index < 0 ? string.Empty : Uri.UnescapeDataString(part[(index + 1)..]);
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nafath refused a call: HTTP {Status}, code {Code}, reference {Reference}")]
    private static partial void LogRefused(ILogger logger, int status, string code, string reference);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A Nafath answer failed verification: {Reason}")]
    private static partial void LogInvalidToken(ILogger logger, string reason);
}
