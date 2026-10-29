extern alias InternalApi;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>A small Keycloak admin API: token, users (search, create, update), passwords, emails, sign-out, federation links.</summary>
internal sealed class FakeKeycloak : HttpMessageHandler
{
    public ConcurrentDictionary<string, JsonObject> Users { get; } = new();

    public ConcurrentQueue<string> Calls { get; } = new();

    public string? RefusePassword { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Enqueue($"{request.Method} {path}");
        if (path.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
        {
            return body!.Contains("client_secret=s3cret", StringComparison.Ordinal) ? Json(new { access_token = "t", expires_in = 300 }) : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (request.Headers.Authorization?.Parameter != "t")
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        var admin = path[(path.IndexOf("/admin/realms/r/", StringComparison.Ordinal) + "/admin/realms/r/".Length)..];
        var parts = admin.Split('/');
        switch (request.Method.Method, parts)
        {
            case ("GET", ["users"]):
                var name = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["username"];
                return Json(Users.Values.Where(u => (string)u["username"]! == name).ToList());
            case ("POST", ["users"]):
                var created = JsonNode.Parse(body!)!.AsObject();
                if (Users.Values.Any(u => (string)u["username"]! == (string)created["username"]!))
                {
                    return Json(new { errorMessage = "User exists with same username" }, HttpStatusCode.Conflict);
                }

                created["id"] = Guid.NewGuid().ToString();
                Users[(string)created["id"]!] = created;
                return new HttpResponseMessage(HttpStatusCode.Created);
            case ("PUT", ["users", var id]):
                foreach (var (key, value) in JsonNode.Parse(body!)!.AsObject())
                {
                    Users[id][key] = value?.DeepClone();
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("PUT", ["users", _, "reset-password"]):
                return RefusePassword is { } refusal ? Json(new { error = "invalidPasswordMinLengthMessage", errorMessage = refusal }, HttpStatusCode.BadRequest) : new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("PUT", ["users", _, "execute-actions-email"]):
            case ("POST", ["users", _, "logout"]):
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("GET", ["users", var id, "federated-identity"]):
                return Json(Users[id]["idp"] is { } idp ? new[] { new { identityProvider = (string)idp! } } : []);
            case ("GET", ["components", _]):
                return Json(new { id = parts[1], name = "Active Directory", providerId = "ldap" });
            case ("GET", ["components"]):
                return Json(new[] { new { id = "c1", name = "Active Directory", providerId = "ldap" } });
            case ("GET", ["identity-provider", "instances"]):
                return Json(new[] { new { alias = "nafath", displayName = "Nafath", enabled = true } });
            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
}

internal sealed class FakeSecrets : ISecretStore
{
    public string? Find(string name, string key) => (name, key) == ("KEYCLOAK_ADMIN", "clientSecret") ? "s3cret" : null;
}

internal sealed class FakeClients(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

public sealed class AccountTests(AppsControlFixture fixture)
{
    private static KeycloakAccountDirectory Directory(FakeKeycloak keycloak, string mode = "Managed") =>
        new(new FakeClients(keycloak), Options.Create(new IdentityOptions { Provider = "Keycloak", Mode = mode, BaseUrl = $"http://kc-{Guid.NewGuid():N}", Realm = "r" }), new FakeSecrets(), TimeProvider.System);

    [Fact]
    public async Task Local_accounts_are_created_with_a_temporary_password_and_can_be_disabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var keycloak = new FakeKeycloak();
        var accounts = Directory(keycloak);

        var created = await accounts.CreateAsync(new NewAccountRequest("Sara.Ali", "sara@example.gov.sa", "Sara", "Ali", "Temp@12345", false), ct);
        created.UserName.ShouldBe("sara.ali");
        created.Enabled.ShouldBeTrue();
        created.Source.ShouldBe("LOCAL");
        created.CanSetPassword.ShouldBeTrue();
        keycloak.Calls.ShouldContain(c => c.EndsWith("/reset-password", StringComparison.Ordinal));

        await Should.ThrowAsync<AccountException>(() => accounts.CreateAsync(new NewAccountRequest("sara.ali", null, null, null, "x", false), ct));

        await accounts.SetEnabledAsync("sara.ali", false, ct);
        (await accounts.GetAsync("sara.ali", ct)).Enabled.ShouldBeFalse();
        keycloak.Calls.ShouldContain(c => c.EndsWith("/logout", StringComparison.Ordinal), "disabling ends the sessions");

        await accounts.SetPasswordAsync("sara.ali", new PasswordRequest(null, true), ct);
        keycloak.Calls.ShouldContain(c => c.EndsWith("/execute-actions-email", StringComparison.Ordinal));

        keycloak.RefusePassword = "Invalid password: minimum length 12.";
        var refused = await Should.ThrowAsync<AccountException>(() => accounts.SetPasswordAsync("sara.ali", new PasswordRequest("short", false), ct));
        refused.Problem.ShouldBe(AccountProblem.Invalid);
        refused.Message.ShouldContain("minimum length");
    }

    [Fact]
    public async Task Directory_and_identity_provider_accounts_keep_their_passwords_there()
    {
        var ct = TestContext.Current.CancellationToken;
        var keycloak = new FakeKeycloak();
        keycloak.Users["1"] = new JsonObject { ["id"] = "1", ["username"] = "ad.user", ["enabled"] = true, ["federationLink"] = "c1" };
        keycloak.Users["2"] = new JsonObject { ["id"] = "2", ["username"] = "nafath.user", ["enabled"] = true, ["idp"] = "nafath" };
        var accounts = Directory(keycloak);

        var ad = await accounts.GetAsync("ad.user", ct);
        ad.Source.ShouldBe("Active Directory");
        ad.CanSetPassword.ShouldBeFalse();
        (await accounts.GetAsync("nafath.user", ct)).Source.ShouldBe("nafath");
        await Should.ThrowAsync<AccountException>(() => accounts.SetPasswordAsync("ad.user", new PasswordRequest("Temp@12345", false), ct));
        await accounts.SetEnabledAsync("ad.user", false, ct);

        var capabilities = await accounts.CapabilitiesAsync(ct);
        capabilities.CanCreate.ShouldBeTrue();
        capabilities.Federations.ShouldBe(["LDAP: Active Directory", "Nafath"]);

        var federated = Directory(keycloak, "Federated");
        (await federated.CapabilitiesAsync(ct)).CanCreate.ShouldBeFalse();
        await Should.ThrowAsync<AccountException>(() => federated.CreateAsync(new NewAccountRequest("new.user", null, null, null, "Temp@12345", false), ct));
    }

    [Fact]
    public async Task Disabling_a_user_blocks_access_at_once_and_needs_the_users_privilege()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        var keycloak = new FakeKeycloak();
        var userName = Harness.Unique("staff").ToLowerInvariant();
        keycloak.Users["s"] = new JsonObject { ["id"] = "s", ["username"] = userName, ["enabled"] = true };
        await using var factory = new WebApplicationFactory<InternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", fixture.ConnectionString!);
            builder.UseSetting("AppsControl:BootstrapUsers:admin:0", "SYSTEM_ADMIN");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
                services.AddScoped<IAccountDirectory>(sp => Directory(keycloak));
            });
        });

        using var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Add(TestAuthHandler.Header, userName);
        (await staff.GetAsync(new Uri("/api/apps-control/v1/me", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "the user is provisioned on first use");
        (await staff.PostAsync(new Uri($"/api/apps-control/v1/admin/accounts/{userName}/disable", UriKind.Relative), null, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.Header, "admin");
        var account = await admin.GetFromJsonAsync<AccountView>(new Uri($"/api/apps-control/v1/admin/accounts/{userName}", UriKind.Relative), ct);
        account!.Enabled.ShouldBeTrue();
        (await admin.PostAsync(new Uri($"/api/apps-control/v1/admin/accounts/{userName}/disable", UriKind.Relative), null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppsControlDbContext>();
            (await db.Users.SingleAsync(u => u.Code == userName, ct)).Status.ShouldBe(UserStatus.Disabled);
        }

        ((bool?)keycloak.Users["s"]["enabled"]).ShouldBe(false);
        (await staff.GetAsync(new Uri("/api/apps-control/v1/me/menu?app=INTERNAL", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a disabled user loses access straight away");

        (await admin.PostAsync(new Uri($"/api/apps-control/v1/admin/accounts/{userName}/enable", UriKind.Relative), null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var invalid = await admin.PostAsJsonAsync(new Uri("/api/apps-control/v1/admin/accounts", UriKind.Relative), new NewAccountRequest("bad name!", null, null, null, "x", false), ct);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
