using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.Nafath;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DynamicCube.Modules.Integration.Nafath;

/// <summary>The bridge's signing key: from the secret store (<c>signingKey</c>, PEM RSA) or, in development, made at start.</summary>
public sealed partial class NafathBridgeKeys
{
    public NafathBridgeKeys(IOptions<NafathOptions> options, ISecretStore secrets, ILogger<NafathBridgeKeys> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(secrets);
        var rsa = RSA.Create();
        if (secrets.Find(options.Value.Bridge.SecretName, "signingKey") is { Length: > 0 } pem)
        {
            rsa.ImportFromPem(pem);
        }
        else
        {
            rsa.KeySize = 2048;
            LogEphemeral(logger, options.Value.Bridge.SecretName);
        }

        var key = new RsaSecurityKey(rsa);
        key.KeyId = Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
        Key = key;
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = key.KeyId });
        Public = new Dictionary<string, object?>
        {
            ["kty"] = jwk.Kty,
            ["use"] = "sig",
            ["alg"] = SecurityAlgorithms.RsaSha256,
            ["kid"] = key.KeyId,
            ["n"] = jwk.N,
            ["e"] = jwk.E,
        };
    }

    public RsaSecurityKey Key { get; }

    /// <summary>The public key as a JWK.</summary>
    public IReadOnlyDictionary<string, object?> Public { get; }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Secrets:{Name}:signingKey is not set: the Nafath sign-in bridge signs with a key made at start (development only; every instance needs the same key)")]
    private static partial void LogEphemeral(ILogger logger, string name);
}

/// <summary>What the bridge does next: send the browser somewhere, or show a page (while Nafath's answer is awaited).</summary>
public sealed record NafathStep(string? RedirectTo, string? Page = null, int Status = 302);

/// <summary>
/// Signing in with Nafath through Keycloak (ADR-0043): to Keycloak the bridge is an OpenID Connect provider. Keycloak sends
/// the browser to <c>authorize</c>; the bridge opens Nafath's web session and redirects to Nafath's page; Nafath sends the
/// browser back to <c>callback</c>; the bridge redeems the state for Nafath's signed answer, checks it, and returns to
/// Keycloak with a one-time code that Keycloak exchanges at <c>token</c> for an ID token naming the person (subject and
/// user name: the national ID). Keycloak then signs the person in, creating their account on first use.
/// </summary>
public sealed partial class NafathSignIn(
    IntegrationDbContext context,
    NafathClient client,
    NafathBridgeKeys keys,
    IOptions<NafathOptions> options,
    ISecretStore secrets,
    IDataProtectionProvider protection,
    TimeProvider clock,
    ILogger<NafathSignIn> logger)
{
    public const string SubjectType = "SIGN_IN";

    /// <summary>How long Keycloak has to redeem a code.</summary>
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(2);

    private readonly IDataProtector _protector = protection.CreateProtector(NafathVerifications.Purpose);

    private NafathBridgeOptions Bridge => options.Value.Bridge;

    public async Task<NafathStep> AuthorizeAsync(string? clientId, string? redirectUri, string? responseType, string? state, string? nonce, string? userIp, CancellationToken cancellationToken)
    {
        if (!string.Equals(clientId, Bridge.ClientId, StringComparison.Ordinal) || redirectUri is null || !Bridge.RedirectUris.Contains(redirectUri, StringComparer.Ordinal))
        {
            // Never redirect to an address that is not registered.
            return new NafathStep(null, Page("لا يمكن متابعة الدخول", "This sign-in cannot continue: the client or its return address is not registered."), 400);
        }

        if (!string.Equals(responseType, "code", StringComparison.Ordinal))
        {
            return new NafathStep(Return(redirectUri, state, error: "unsupported_response_type"));
        }

        var now = clock.GetUtcNow();
        var request = new NafathRequest
        {
            Kind = NafathRequestKind.SignIn,
            Status = NafathRequestStatus.Waiting,
            Service = "Login",
            SubjectType = SubjectType,
            ClientRedirectUri = redirectUri,
            ClientState = state,
            ClientNonce = nonce,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(options.Value.RequestSeconds).AddMinutes(5),
        };
        try
        {
            var session = await client.CreateWebSessionAsync(request.Id.ToString(), userIp, cancellationToken);
            request.ProtectedState = _protector.Protect(session.State);
            request.HashedState = session.HashedState;
            context.NafathRequests.Add(request);
            await context.SaveChangesAsync(cancellationToken);
            return new NafathStep(session.Url);
        }
        catch (NafathException ex)
        {
            LogFailed(logger, ex.Message);
            return new NafathStep(Return(redirectUri, state, error: "temporarily_unavailable"));
        }
    }

    /// <summary>Nafath sends the browser back here with the state (or its hash).</summary>
    public async Task<NafathStep> CallbackAsync(string? state, string? hashedState, string? userIp, CancellationToken cancellationToken)
    {
        var candidates = new[] { state is null ? null : NafathClient.Digest(state), hashedState, state }.OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var request = candidates.Count == 0
            ? null
            : await context.NafathRequests.FirstOrDefaultAsync(r => r.Kind == NafathRequestKind.SignIn && r.HashedState != null && candidates.Contains(r.HashedState), cancellationToken);
        if (request is null)
        {
            return new NafathStep(null, Page("انتهت جلسة الدخول", "This Nafath sign-in is not known or has ended. Start signing in again."), 400);
        }

        if (request.Status != NafathRequestStatus.Waiting || clock.GetUtcNow() > request.ExpiresAt)
        {
            if (request.Status == NafathRequestStatus.Waiting)
            {
                request.Status = NafathRequestStatus.Expired;
                await context.SaveChangesAsync(cancellationToken);
            }

            return new NafathStep(Return(request.ClientRedirectUri!, request.ClientState, error: "access_denied"));
        }

        string? token;
        try
        {
            token = await client.RedeemWebTokenAsync(_protector.Unprotect(request.ProtectedState!), userIp, cancellationToken);
        }
        catch (NafathException ex)
        {
            LogFailed(logger, ex.Message);
            request.Status = NafathRequestStatus.Rejected;
            await context.SaveChangesAsync(cancellationToken);
            return new NafathStep(Return(request.ClientRedirectUri!, request.ClientState, error: "access_denied"));
        }

        if (token is null)
        {
            // Not answered yet: wait here and ask again.
            return new NafathStep(null, Page("بانتظار نفاذ", "Waiting for Nafath… approve the request in the Nafath app.", refresh: true), 200);
        }

        var (person, _) = await client.VerifyAsync(token, cancellationToken);
        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        request.CodeHash = Hash(code);
        request.ProtectedPerson = _protector.Protect(JsonSerializer.Serialize(person));
        request.ProtectedState = null;
        request.Status = NafathRequestStatus.Verified;
        request.CompletedAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        return new NafathStep(Return(request.ClientRedirectUri!, request.ClientState, code: code));
    }

    /// <summary>Keycloak exchanges the code (once, within two minutes) for the ID token; null when it may not.</summary>
    public async Task<IReadOnlyDictionary<string, object>?> TokenAsync(string? clientId, string? clientSecret, string? code, string? redirectUri, string issuer, CancellationToken cancellationToken)
    {
        var expected = secrets.Find(Bridge.SecretName, "clientSecret");
        if (string.IsNullOrEmpty(expected) || !string.Equals(clientId, Bridge.ClientId, StringComparison.Ordinal)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(clientSecret ?? string.Empty), Encoding.UTF8.GetBytes(expected)))
        {
            return null;
        }

        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        var hash = Hash(code);
        var request = await context.NafathRequests.SingleOrDefaultAsync(r => r.CodeHash == hash && r.Kind == NafathRequestKind.SignIn, cancellationToken);
        var now = clock.GetUtcNow();
        if (request is not { Status: NafathRequestStatus.Verified, CompletedAt: not null, ProtectedPerson: not null }
            || now - request.CompletedAt > CodeLifetime
            || !string.Equals(request.ClientRedirectUri, redirectUri, StringComparison.Ordinal))
        {
            return null;
        }

        var person = JsonSerializer.Deserialize<NafathPerson>(_protector.Unprotect(request.ProtectedPerson))!;
        request.Status = NafathRequestStatus.Used;
        request.ProtectedPerson = null;
        request.CodeHash = null;
        await context.SaveChangesAsync(cancellationToken);

        var names = (person.FullNameAr ?? person.FullNameEn ?? person.Identifier).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var claims = new Dictionary<string, object>
        {
            ["sub"] = person.Identifier,
            ["preferred_username"] = person.Identifier,
            ["national_id"] = person.Identifier,
            ["name"] = person.FullNameAr ?? person.FullNameEn ?? person.Identifier,
            ["given_name"] = names[0],
            ["family_name"] = names.Length > 1 ? names[^1] : names[0],
            ["auth_time"] = (request.CompletedAt ?? now).ToUnixTimeSeconds(),
            ["amr"] = new[] { "nafath" },
        };
        if (person.FullNameEn is not null)
        {
            claims["name_en"] = person.FullNameEn;
        }

        if (request.ClientNonce is not null)
        {
            claims["nonce"] = request.ClientNonce;
        }

        var idToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = Bridge.ClientId,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(5).UtcDateTime,
            SigningCredentials = new SigningCredentials(keys.Key, SecurityAlgorithms.RsaSha256),
        });
        return new Dictionary<string, object>
        {
            ["access_token"] = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)),
            ["token_type"] = "Bearer",
            ["expires_in"] = 300,
            ["id_token"] = idToken,
        };
    }

    private static string Hash(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Return(string redirectUri, string? state, string? code = null, string? error = null)
    {
        var query = new List<string>();
        if (code is not null)
        {
            query.Add("code=" + Uri.EscapeDataString(code));
        }

        if (error is not null)
        {
            query.Add("error=" + Uri.EscapeDataString(error));
        }

        if (state is not null)
        {
            query.Add("state=" + Uri.EscapeDataString(state));
        }

        return redirectUri + (redirectUri.Contains('?', StringComparison.Ordinal) ? "&" : "?") + string.Join('&', query);
    }

    /// <summary>A small bilingual page (Arabic first), refreshing itself while Nafath's answer is awaited.</summary>
    private static string Page(string arabic, string english, bool refresh = false) =>
        $$"""
        <!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        {{(refresh ? "<meta http-equiv=\"refresh\" content=\"2\">" : string.Empty)}}<title>نفاذ — Nafath</title>
        <style>body{font-family:system-ui,'IBM Plex Sans Arabic',sans-serif;display:grid;place-items:center;min-height:100vh;margin:0;background:#f9fafb;color:#161616}
        main{max-width:28rem;padding:24px;border:1px solid #e5e7eb;border-radius:12px;background:#fff;text-align:center}p{color:#4d5761}</style></head>
        <body><main><h1>{{System.Net.WebUtility.HtmlEncode(arabic)}}</h1><p dir="ltr">{{System.Net.WebUtility.HtmlEncode(english)}}</p></main></body></html>
        """;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nafath sign-in failed: {Reason}")]
    private static partial void LogFailed(ILogger logger, string reason);
}
