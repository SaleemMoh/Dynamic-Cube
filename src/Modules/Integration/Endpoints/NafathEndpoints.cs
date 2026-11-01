using System.Text;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Integration.Nafath;
using DynamicCube.Platform.Nafath;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.Integration.Endpoints;

/// <summary>Nafath's signed answer to a verification in the app, as Nafath posts it.</summary>
public sealed record NafathCallback(string? Token, string? TransId, string? RequestId);

/// <summary>
/// Nafath on the external host (ADR-0043): the sign-in bridge Keycloak uses as an OpenID Connect provider
/// (<c>/oidc/…</c>: discovery, authorize, Nafath's return, token, keys) and the callback where Nafath posts the signed
/// answer of a verification. All anonymous: each step checks what it receives (registered client and return address,
/// state, one-time code with the client secret, Nafath's signature).
/// </summary>
internal static class NafathEndpoints
{
    public const string Path = "/api/nafath/v1";

    public static void Map(RouteGroupBuilder group)
    {
        var oidc = group.MapGroup("/oidc");
        oidc.MapGet("/.well-known/openid-configuration", (HttpContext http, IOptions<NafathOptions> options) =>
        {
            var issuer = Issuer(http, options.Value);
            return Results.Json(new JsonObject
            {
                ["issuer"] = issuer,
                ["authorization_endpoint"] = issuer + "/authorize",
                ["token_endpoint"] = issuer + "/token",
                ["jwks_uri"] = issuer + "/jwks",
                ["response_types_supported"] = new JsonArray("code"),
                ["subject_types_supported"] = new JsonArray("public"),
                ["id_token_signing_alg_values_supported"] = new JsonArray("RS256"),
                ["token_endpoint_auth_methods_supported"] = new JsonArray("client_secret_post", "client_secret_basic"),
                ["scopes_supported"] = new JsonArray("openid"),
                ["claims_supported"] = new JsonArray("sub", "preferred_username", "national_id", "name", "given_name", "family_name"),
            });
        }).WithName("NafathDiscovery");

        oidc.MapGet("/authorize", async (
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "client_id")] string? clientId,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "redirect_uri")] string? redirectUri,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "response_type")] string? responseType,
            string? state,
            string? nonce,
            HttpContext http,
            NafathSignIn signIn,
            CancellationToken ct) => Step(await signIn.AuthorizeAsync(clientId, redirectUri, responseType, state, nonce, UserIp(http), ct)))
            .WithName("NafathAuthorize");

        // Where Nafath sends the browser back (the callback registered with Elm for the web sign-in).
        oidc.MapGet("/callback", async (string? state, string? hashedState, HttpContext http, NafathSignIn signIn, CancellationToken ct) =>
            Step(await signIn.CallbackAsync(state, hashedState, UserIp(http), ct))).WithName("NafathReturn");

        oidc.MapPost("/token", async (HttpContext http, NafathSignIn signIn, IOptions<NafathOptions> options, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var (clientId, clientSecret) = ClientCredentials(http, form);
            if (!string.Equals(form["grant_type"], "authorization_code", StringComparison.Ordinal))
            {
                return Error(400, "unsupported_grant_type");
            }

            var tokens = await signIn.TokenAsync(clientId, clientSecret, form["code"], form["redirect_uri"], Issuer(http, options.Value), ct);
            http.Response.Headers.CacheControl = "no-store";
            return tokens is null ? Error(400, "invalid_grant") : Results.Json(tokens);
        }).DisableAntiforgery().WithName("NafathToken");

        oidc.MapGet("/jwks", (NafathBridgeKeys keys) => Results.Json(new { keys = new[] { keys.Public } })).WithName("NafathKeys");

        // Nafath posts the signed answer of a verification in the app here.
        group.MapPost("/callbacks/mfa", async (NafathCallback body, NafathClient client, NafathVerifications verifications, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(body.Token) || !Guid.TryParse(body.RequestId, out var requestId))
            {
                return Results.BadRequest();
            }

            try
            {
                var (person, transId) = await client.VerifyAsync(body.Token, ct);
                return await verifications.AnswerAsync(requestId, transId ?? body.TransId, person, ct) ? Results.Ok() : Results.BadRequest();
            }
            catch (NafathException)
            {
                return Results.BadRequest();
            }
        }).WithName("NafathVerificationAnswer");
    }

    private static IResult Step(NafathStep step) =>
        step.RedirectTo is not null ? Results.Redirect(step.RedirectTo) : Results.Content(step.Page ?? string.Empty, "text/html; charset=utf-8", Encoding.UTF8, step.Status);

    private static string Issuer(HttpContext http, NafathOptions options) =>
        options.Bridge.Issuer is { Length: > 0 } issuer ? issuer.TrimEnd('/') : $"{http.Request.Scheme}://{http.Request.Host}{Path}/oidc";

    private static string? UserIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static (string? Id, string? Secret) ClientCredentials(HttpContext http, IFormCollection form)
    {
        var header = http.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var pair = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
                var colon = pair.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    return (Uri.UnescapeDataString(pair[..colon]), Uri.UnescapeDataString(pair[(colon + 1)..]));
                }
            }
            catch (FormatException)
            {
                return (null, null);
            }
        }

        return (form["client_id"], form["client_secret"]);
    }

    private static IResult Error(int status, string error) => Results.Json(new { error }, statusCode: status);
}
