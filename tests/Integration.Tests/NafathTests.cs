extern alias ExternalApi;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Integration.Nafath;
using DynamicCube.Platform.Nafath;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace DynamicCube.Modules.Integration.Tests;

/// <summary>
/// Nafath (ADR-0043) on the external host against a stand-in for Elm's Rabet API: signing in through the bridge Keycloak
/// uses (Nafath's page and back, a one-time code, a signed ID token) and verifications in the app (the number, the
/// status, the signed answer at the callback).
/// </summary>
public sealed class NafathTests(DatabaseFixture fixture) : IAsyncDisposable
{
    private const string Redirect = "https://sso.test/realms/r/broker/nafath/endpoint";
    private const string Issuer = "https://portal.test/api/nafath/v1/oidc";

    private readonly FakeRabet _rabet = new();
    private WebApplicationFactory<ExternalApi::Program>? _factory;

    private WebApplicationFactory<ExternalApi::Program> Factory => _factory ??= new WebApplicationFactory<ExternalApi::Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DynamicCube", fixture.RequireConnectionString());
        builder.UseSetting("Nafath:BaseUrl", "https://rabet.test/stg");
        builder.UseSetting("Nafath:ServerIp", "10.9.9.9");
        builder.UseSetting("Secrets:NAFATH:appId", "app-id");
        builder.UseSetting("Secrets:NAFATH:appKey", "app-key");
        builder.UseSetting("Nafath:Bridge:Issuer", Issuer);
        builder.UseSetting("Nafath:Bridge:RedirectUris:0", Redirect);
        builder.UseSetting("Secrets:NAFATH_BRIDGE:clientSecret", "bridge-secret");
        builder.ConfigureTestServices(services => services.AddHttpClient(NafathClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _rabet));
    });

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        _rabet.Dispose();
    }

    [Fact]
    public async Task Signing_in_goes_through_nafaths_page_and_gives_keycloak_a_signed_id_token_once()
    {
        var ct = TestContext.Current.CancellationToken;
        using var browser = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var discovery = await browser.GetFromJsonAsync<JsonObject>("/api/nafath/v1/oidc/.well-known/openid-configuration", ct);
        discovery!["token_endpoint"]!.GetValue<string>().ShouldBe(Issuer + "/token");

        // Keycloak sends the browser here; the bridge opens Nafath's session and sends it to Nafath's page.
        var authorize = await browser.GetAsync(new Uri($"/api/nafath/v1/oidc/authorize?client_id=keycloak&response_type=code&state=kc-state&nonce=n-1&redirect_uri={Uri.EscapeDataString(Redirect)}", UriKind.Relative), ct);
        authorize.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var page = authorize.Headers.Location!;
        page.Host.ShouldBe("nafath-portal.test");
        var state = Uri.UnescapeDataString(page.Query.Split("state=")[1].Split('&')[0]);
        state.ShouldContain("+", Case.Sensitive, "the state is base64 and keeps its +");
        _rabet.Calls.ShouldContain(c => c.StartsWith("GET /stg/api/v2/oidc/session", StringComparison.Ordinal));

        // Back before the person answered: a page that waits and asks again.
        var early = await browser.GetAsync(new Uri($"/api/nafath/v1/oidc/callback?state={Uri.EscapeDataString(state)}", UriKind.Relative), ct);
        early.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await early.Content.ReadAsStringAsync(ct)).ShouldContain("http-equiv=\"refresh\"");

        _rabet.ApproveWeb(state, "1000000001");
        var back = await browser.GetAsync(new Uri($"/api/nafath/v1/oidc/callback?state={Uri.EscapeDataString(state)}", UriKind.Relative), ct);
        back.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        back.Headers.Location!.ToString().ShouldStartWith(Redirect + "?code=");
        back.Headers.Location!.Query.ShouldContain("state=kc-state");
        var code = Uri.UnescapeDataString(back.Headers.Location!.Query.Split("code=")[1].Split('&')[0]);

        // Keycloak exchanges the code with the client secret, once.
        Task<HttpResponseMessage> Exchange(string secret) => browser.PostAsync(new Uri("/api/nafath/v1/oidc/token", UriKind.Relative), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = Redirect,
            ["client_id"] = "keycloak",
            ["client_secret"] = secret,
        }), ct);
        (await Exchange("wrong")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var tokens = await (await Exchange("bridge-secret")).Content.ReadFromJsonAsync<JsonObject>(ct);
        var idToken = tokens!["id_token"]!.GetValue<string>();
        (await Exchange("bridge-secret")).StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a code is used once");

        var jwks = await browser.GetStringAsync(new Uri("/api/nafath/v1/oidc/jwks", UriKind.Relative), ct);
        var checkedToken = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            IssuerSigningKeys = new JsonWebKeySet(jwks).GetSigningKeys(),
            ValidIssuer = Issuer,
            ValidAudience = "keycloak",
        });
        checkedToken.IsValid.ShouldBeTrue();
        checkedToken.Claims["sub"].ShouldBe("1000000001");
        checkedToken.Claims["preferred_username"].ShouldBe("1000000001");
        checkedToken.Claims["nonce"].ShouldBe("n-1");
        checkedToken.Claims["given_name"].ShouldBe("محمد");

        // Never a redirect to an address that is not registered.
        var foreign = await browser.GetAsync(new Uri("/api/nafath/v1/oidc/authorize?client_id=keycloak&response_type=code&state=x&redirect_uri=https%3A%2F%2Fevil.test%2Fcb", UriKind.Relative), ct);
        foreign.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        foreign.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task A_verification_shows_a_number_and_is_verified_only_by_the_signed_answer_naming_the_person()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Factory.Services.CreateScope();
        var nafath = scope.ServiceProvider.GetRequiredService<INafathVerifications>();
        nafath.IsConfigured.ShouldBeTrue();
        var subject = Guid.CreateVersion7();

        // Each check in its own scope, as each request of the applicant's page is.
        async Task<NafathCheck> Check(Guid id)
        {
            using var fresh = Factory.Services.CreateScope();
            return (await fresh.ServiceProvider.GetRequiredService<INafathVerifications>().CheckAsync(id, null, ct))!;
        }

        var challenge = await nafath.StartAsync("1000000001", null, "TEST", subject, "1.2.3.4", ct);
        challenge.Random.ShouldBe(_rabet.Requests.Values.Single(r => r.NationalId == "1000000001").Random);
        _rabet.Headers.ShouldContain(h => h == "app-id|app-key|1.2.3.4, 10.9.9.9", "credentials from the secret store; the person, then this server");
        (await Check(challenge.Id)).Status.ShouldBe(NafathStatus.Waiting);
        (await Should.ThrowAsync<NafathException>(() => nafath.StartAsync("1000000001", null, "TEST", subject, null, ct))).Code.ShouldBe("400-034-050");

        using var api = Factory.CreateClient();
        var request = _rabet.Requests.Values.Single(r => r.NationalId == "1000000001");
        async Task<HttpStatusCode> Callback(string token) => (await api.PostAsJsonAsync("/api/nafath/v1/callbacks/mfa", new { token, transId = request.TransId, requestId = challenge.Id.ToString() }, ct)).StatusCode;
        (await Callback(_rabet.Token("1999999999", request.TransId))).ShouldBe(HttpStatusCode.BadRequest, "someone else's answer");
        (await Callback(FakeRabet.ForeignToken("1000000001", request.TransId))).ShouldBe(HttpStatusCode.BadRequest, "not signed by Nafath");
        (await Check(challenge.Id)).Status.ShouldBe(NafathStatus.Waiting);

        (await Callback(_rabet.Token("1000000001", request.TransId))).ShouldBe(HttpStatusCode.OK);
        var verified = await Check(challenge.Id);
        verified.Status.ShouldBe(NafathStatus.Verified);
        verified.SubjectId.ShouldBe(subject);
        verified.Person!.Identifier.ShouldBe("1000000001");
        verified.Person.FullNameAr.ShouldBe("محمد عبدالله العتيبي");

        var other = await nafath.StartAsync("1000000003", "Login", "TEST", null, null, ct);
        _rabet.Reject("1000000003");
        (await Check(other.Id)).Status.ShouldBe(NafathStatus.Rejected);
    }

    /// <summary>Answers like Elm's Rabet API for the stand-in's sessions and requests, signed with its own key.</summary>
    private sealed class FakeRabet : HttpMessageHandler
    {
        private static readonly RsaSecurityKey Foreign = new(RSA.Create(2048)) { KeyId = "foreign" };
        private readonly RsaSecurityKey _key = new(RSA.Create(2048)) { KeyId = "rabet-1" };
        private readonly ConcurrentDictionary<string, string?> _sessions = new(StringComparer.Ordinal);

        public ConcurrentQueue<string> Calls { get; } = new();

        public ConcurrentQueue<string> Headers { get; } = new();

        public ConcurrentDictionary<string, (string NationalId, string TransId, string Random, string Status)> Requests { get; } = new();

        public void ApproveWeb(string state, string nationalId) => _sessions[state] = nationalId;

        public void Reject(string nationalId)
        {
            var request = Requests.Single(r => r.Value.NationalId == nationalId);
            Requests[request.Key] = request.Value with { Status = "REJECTED" };
        }

        public string Token(string nationalId, string? transId) => Sign(_key, nationalId, transId);

        public static string ForeignToken(string nationalId, string? transId) => Sign(Foreign, nationalId, transId);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Calls.Enqueue($"{request.Method} {path}");
            Headers.Enqueue($"{Header(request, "APP-ID")}|{Header(request, "APP-KEY")}|{Header(request, "X-Forwarded-For")}");
            if (Header(request, "APP-ID") != "app-id" || Header(request, "APP-KEY") != "app-key")
            {
                return Json(HttpStatusCode.Forbidden, new { code = "403-000-001" });
            }

            var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            switch (path)
            {
                case "/stg/api/v2/oidc/session":
                    var state = "s+" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
                    _sessions[state] = null;
                    return Json(HttpStatusCode.OK, new { url = $"https://nafath-portal.test/login?state={Uri.EscapeDataString(state)}", hashedState = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(state))) });
                case "/stg/api/v2/oidc/jwt":
                    var given = body!["state"]!.GetValue<string>();
                    return !_sessions.TryGetValue(given, out var who) ? Json(HttpStatusCode.BadRequest, new { code = "400-034-053" })
                        : who is null ? Json(HttpStatusCode.Unauthorized, new { code = "401-033-024" })
                        : Json(HttpStatusCode.OK, new { token = Token(who, null) });
                case "/stg/api/v1/mfa/request":
                    var id = body!["nationalId"]!.GetValue<string>();
                    if (Requests.Values.Any(r => r.NationalId == id && r.Status == "WAITING"))
                    {
                        return Json(HttpStatusCode.BadRequest, new { code = "400-034-050" });
                    }

                    var trans = Guid.NewGuid().ToString();
                    Requests[trans] = (id, trans, Random.Shared.Next(10, 100).ToString(System.Globalization.CultureInfo.InvariantCulture), "WAITING");
                    return Json(HttpStatusCode.OK, new { transId = trans, random = Requests[trans].Random });
                case "/stg/api/v1/mfa/request/status":
                    return Json(HttpStatusCode.OK, new { status = Requests[body!["transId"]!.GetValue<string>()].Status });
                case "/stg/api/v1/mfa/jwk":
                    var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(_key);
                    return Json(HttpStatusCode.OK, new { keys = new[] { new { kty = jwk.Kty, kid = _key.KeyId, use = "sig", n = jwk.N, e = jwk.E } } });
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private static string Sign(RsaSecurityKey key, string nationalId, string? transId)
        {
            var claims = new Dictionary<string, object> { ["nin"] = nationalId, ["firstName"] = "محمد", ["fatherName"] = "عبدالله", ["familyName"] = "العتيبي", ["englishFirstName"] = "Mohammed" };
            if (transId is not null)
            {
                claims["transId"] = transId;
            }

            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Claims = claims, Expires = DateTime.UtcNow.AddMinutes(5), SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256) });
        }

        private static string Header(HttpRequestMessage request, string name) =>
            request.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : string.Empty;

        private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    }
}
