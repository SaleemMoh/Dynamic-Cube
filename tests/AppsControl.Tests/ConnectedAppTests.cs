extern alias InternalApi;

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.ConnectedApps;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>
/// Connected apps (ADR-0036 step 7): their registration (sign-in client, return addresses, own screens and menu tree),
/// their sign-in client in Keycloak, and the connected-app API that serves them their users' access.
/// </summary>
public sealed class ConnectedAppTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private const string Audience = ConnectedAppAuthorization.DefaultAudience;

    private Harness _harness = null!;
    private WebApplicationFactory<InternalApi::Program> _factory = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        var connectionString = fixture.ConnectionString;
        _factory = new WebApplicationFactory<InternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none");
            builder.UseSetting("AppsControl:AccessCacheSeconds", "1");
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TokenAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TokenAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TokenAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TokenAuthHandler>(TokenAuthHandler.SchemeName, null));
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _harness.DisposeAsync();
    }

    private static Change App(string code, object? extra = null)
    {
        var payload = new JsonObject
        {
            ["nameAr"] = "نظام",
            ["nameEn"] = "System " + code,
            ["kind"] = "CONNECTED",
            ["companies"] = new JsonArray("*"),
            ["branches"] = new JsonArray("*"),
        };
        foreach (var (key, value) in JsonSerializer.SerializeToNode(extra ?? new { })!.AsObject())
        {
            payload[key] = value?.DeepClone();
        }

        return new Change(AppsControlChangeTypes.BusinessApp, code, ChangeOperation.Create, payload);
    }

    private static Change Screen(string code, string app, string? channel = null, string? parent = null, params string[] actions) =>
        new(AppsControlChangeTypes.Resource, code, ChangeOperation.Create, new
        {
            nameAr = "شاشة",
            nameEn = "Screen " + code,
            businessApp = app,
            appCode = channel,
            parentCode = parent,
            resourceType = "SCREEN",
            availableActions = actions.Length == 0 ? ["VIEW"] : actions,
        });

    private static Change Item(string code, string? app, string? channel, string? group, string? resource) =>
        new(AppsControlChangeTypes.Menu, code, ChangeOperation.Create, new
        {
            nameAr = "عنصر",
            nameEn = "Item " + code,
            businessApp = app,
            appCode = channel,
            groupCode = group,
            itemType = "SCREEN",
            target = "/" + code.ToLowerInvariant(),
            resourceCode = resource,
            isVisible = channel is null,
        });

    private async Task<IEnumerable<string>> Problems(string area, params Change[] changes) =>
        (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(area, changes))).Errors.Keys;

    private async Task<JsonElement> RowAsync(string entityType, string code) =>
        (await _harness.AsAsync("admin", sp => sp.GetServices<IConfigApplier>().Single(a => a.EntityType == entityType).GetAsync(code, CancellationToken.None)))!.Value;

    [Fact]
    public async Task A_connected_app_signs_in_with_its_own_client_and_keeps_its_screens_and_menu_to_itself()
    {
        var (app, other) = (Harness.Unique("PMS"), Harness.Unique("HRV"));
        await _harness.PublishAsync(AppsControlAreas.System, App(app, new { baseUrl = "https://pms.example.gov.sa/app/" }), App(other));

        var row = await RowAsync(AppsControlChangeTypes.BusinessApp, app);
        row.GetProperty("clientId").GetString().ShouldBe("dc-app-" + app.ToLowerInvariant(), "the default client is dc-app- and the code");
        row.GetProperty("redirectUris").EnumerateArray().Select(u => u.GetString()).ShouldBe(["https://pms.example.gov.sa/app/*"], "users return to the app's address");

        (await Problems(AppsControlAreas.System, App(Harness.Unique("X"), new { clientId = "dynamic-cube-internal-web" }))).ShouldContain("clientId", "the platform's own clients are not for apps");
        (await Problems(AppsControlAreas.System, App(Harness.Unique("X"), new { clientId = "dc-app-" + app.ToLowerInvariant() }))).ShouldContain("clientId", "one client, one app");
        (await Problems(AppsControlAreas.System, App(Harness.Unique("X"), new { redirectUris = new[] { "ftp://files.example/*" } }))).ShouldContain("redirectUris");
        (await Problems(AppsControlAreas.System, new Change(AppsControlChangeTypes.BusinessApp, Harness.Unique("X"), ChangeOperation.Create, new
        {
            nameAr = "تطبيق",
            nameEn = "App",
            kind = "DYNAMIC_CUBE",
            channels = new[] { "INTERNAL" },
            companies = new[] { "*" },
            branches = new[] { "*" },
            clientId = "dc-app-x",
        }))).ShouldContain("clientId", "a Dynamic Cube app signs in through the platform's own apps");

        // Screens: a connected app's have no channel, and stay under the app's own screens.
        var (projects, staff) = (Harness.Unique("PMS.PROJECTS"), Harness.Unique("HRV.STAFF"));
        await _harness.PublishAsync(AppsControlAreas.Security, Screen(projects, app, actions: ["VIEW", "EDIT"]), Screen(staff, other));
        (await Problems(AppsControlAreas.Security, Screen(Harness.Unique("PMS.X"), app, channel: "INTERNAL"))).ShouldContain("appCode");
        (await Problems(AppsControlAreas.Security, Screen(Harness.Unique("HRV.X"), other, parent: projects))).ShouldContain("parentCode");

        // Menus: the app's tree opens its own screens; a channel's tree never opens a connected app's screens.
        var group = Harness.Unique("PMS_G");
        await _harness.PublishAsync(AppsControlAreas.Menus,
            new Change(AppsControlChangeTypes.MenuGroup, group, ChangeOperation.Create, new { nameAr = "مجموعة", nameEn = "Group", businessApp = app }),
            Item(Harness.Unique("PMS_M"), app, null, group, projects));
        (await Problems(AppsControlAreas.Menus, Item(Harness.Unique("PMS_M"), app, null, group, staff))).ShouldContain("resourceCode", "another app's screen");
        (await Problems(AppsControlAreas.Menus, Item(Harness.Unique("INT_M"), null, "INTERNAL", null, projects))).ShouldContain("resourceCode", "a connected app's screen on a channel");
    }

    [Fact]
    public async Task The_connected_app_api_serves_the_apps_own_menu_screens_and_lists_to_its_signed_in_users_only()
    {
        var (app, other, company, branch, elsewhere) = (Harness.Unique("PMS"), Harness.Unique("HRV"), Harness.Unique("CO"), Harness.Unique("BR"), Harness.Unique("CO2"));
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, company, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Company" }),
            new Change(AppsControlChangeTypes.Company, elsewhere, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Elsewhere" }),
            new Change(AppsControlChangeTypes.Branch, branch, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Branch", companyCode = company }));
        await _harness.PublishAsync(AppsControlAreas.System,
            App(app, new { companies = new[] { company }, branches = new[] { branch } }),
            App(other));
        var (projects, budget, reports, staff) = (Harness.Unique("PMS.PROJECTS"), Harness.Unique("PMS.BUDGET"), Harness.Unique("PMS.REPORTS"), Harness.Unique("HRV.STAFF"));
        var (manager, hr) = (Harness.Unique("PMS_MANAGER"), Harness.Unique("HRV_ROLE"));
        await _harness.PublishAsync(AppsControlAreas.Security,
            Screen(projects, app, actions: ["VIEW", "CREATE", "EDIT"]),
            Screen(budget, app, parent: projects, actions: ["VIEW", "APPROVE"]),
            Screen(reports, app, actions: ["VIEW", "EXPORT"]),
            Screen(staff, other),
            new Change(AppsControlChangeTypes.Role, manager, ChangeOperation.Create, new
            {
                nameAr = "مدير",
                nameEn = "Manager",
                businessApp = app,
                privileges = new[] { new { resourceCode = projects, actions = new[] { "VIEW", "EDIT" } }, new { resourceCode = budget, actions = new[] { "VIEW" } } },
            }),
            new Change(AppsControlChangeTypes.Role, hr, ChangeOperation.Create, new
            {
                nameAr = "موارد",
                nameEn = "HR",
                businessApp = other,
                privileges = new[] { new { resourceCode = staff, actions = new[] { "VIEW" } } },
            }));
        var (user, outsider) = (Harness.Unique("pm").ToLowerInvariant(), Harness.Unique("hr").ToLowerInvariant());
        await _harness.PublishAsync(AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
            {
                nameAr = "مدير مشاريع",
                nameEn = "Projects manager",
                email = user + "@example.gov.sa",
                roles = new object[] { new { roleCode = manager, companyCode = company, branches = new[] { branch } }, new { roleCode = hr, companyCode = "*", branches = new[] { "*" } } },
            }),
            new Change(AppsControlChangeTypes.User, outsider, ChangeOperation.Create, new
            {
                nameAr = "موظف",
                nameEn = "Staff",
                roles = new[] { new { roleCode = hr, companyCode = elsewhere, branches = new[] { "*" } } },
            }));
        var (group, itemProjects, itemReports, itemHelp) = (Harness.Unique("PMS_G"), Harness.Unique("PMS_P"), Harness.Unique("PMS_R"), Harness.Unique("PMS_H"));
        await _harness.PublishAsync(AppsControlAreas.Menus,
            new Change(AppsControlChangeTypes.MenuGroup, group, ChangeOperation.Create, new { nameAr = "مجموعة", nameEn = "Group", businessApp = app }),
            Item(itemProjects, app, null, group, projects),
            Item(itemReports, app, null, group, reports),
            Item(itemHelp, app, null, group, null));
        var (ownList, otherList, entityList) = (Harness.Unique("PMS_LIST"), Harness.Unique("HRV_LIST"), Harness.Unique("ORG_LIST"));
        var listClass = Harness.Unique("PRIORITY");
        await _harness.PublishAsync(AppsControlAreas.Lookups, new Change(AppsControlChangeTypes.Class, listClass, ChangeOperation.Create, new { nameAr = "الأولوية", nameEn = "Priority" }));
        await _harness.PublishAsync(AppsControlAreas.Lookups, new Change(AppsControlChangeTypes.Domain, listClass + "/HIGH", ChangeOperation.Create, new { nameAr = "عالية", nameEn = "High" }));
        await _harness.PublishAsync(AppsControlAreas.Lovs,
            new Change(AppsControlChangeTypes.Lov, ownList, ChangeOperation.Create, new { nameAr = "قائمة", nameEn = "Own", sourceType = "CLASS", classCode = listClass, businessApp = app }),
            new Change(AppsControlChangeTypes.Lov, otherList, ChangeOperation.Create, new { nameAr = "قائمة", nameEn = "Other", sourceType = "CLASS", classCode = listClass, businessApp = other }),
            new Change(AppsControlChangeTypes.Lov, entityList, ChangeOperation.Create, new { nameAr = "قائمة", nameEn = "Organization", sourceType = "CLASS", classCode = listClass, businessApp = "" }));

        var clientId = "dc-app-" + app.ToLowerInvariant();
        using var http = _factory.CreateClient();
        async Task<(HttpStatusCode Status, JsonElement Body)> Get(string path, string? userName, string? client, string audience = Audience, string? company = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
            request.Headers.Add(TokenAuthHandler.UserHeader, userName ?? "service-account-" + client);
            if (client is not null)
            {
                request.Headers.Add(TokenAuthHandler.ClientHeader, client);
            }

            request.Headers.Add(TokenAuthHandler.AudienceHeader, audience);
            if (company is not null)
            {
                request.Headers.Add(WorkspaceHeaders.Company, company);
            }

            using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return (response.StatusCode, text.Length > 0 ? JsonDocument.Parse(text).RootElement.Clone() : default);
        }

        // The token decides the app: an unknown client, the internal system's token, or the app's token elsewhere are refused.
        (await Get("/api/connect/v1/me", user, "dc-app-unknown")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await Get("/api/connect/v1/me", user, clientId, audience: "dynamic-cube-internal-api")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await Get("/api/apps-control/v1/me", user, clientId)).Status.ShouldBe(HttpStatusCode.Forbidden, "a connected app's token reaches only the connected-app API");

        // The app's own token reads its registration, not users' data.
        var registration = await Get("/api/connect/v1/app", null, clientId);
        registration.Status.ShouldBe(HttpStatusCode.OK);
        registration.Body.GetProperty("screens").EnumerateArray().Select(s => s.GetProperty("code").GetString()).ShouldBe([budget, projects, reports], ignoreOrder: true);
        registration.Body.GetProperty("menu")[0].GetProperty("items").GetArrayLength().ShouldBe(3, "the whole tree, for the app");
        (await Get("/api/connect/v1/me", null, clientId)).Status.ShouldBe(HttpStatusCode.Forbidden);

        // A signed-in user: their company and branch among the app's, their menu and screens of this app only.
        var me = await Get("/api/connect/v1/me", user, clientId);
        me.Status.ShouldBe(HttpStatusCode.OK);
        me.Body.GetProperty("hasAccess").GetBoolean().ShouldBeTrue();
        me.Body.GetProperty("company").GetProperty("code").GetString().ShouldBe(company, "the first of the user's companies that uses the app");
        me.Body.GetProperty("companies").EnumerateArray().Select(c => c.GetProperty("code").GetString()).ShouldBe([company]);
        me.Body.GetProperty("branches").EnumerateArray().Select(b => b.GetProperty("code").GetString()).ShouldBe([branch]);
        me.Body.GetProperty("email").GetString().ShouldBe(user + "@example.gov.sa");

        var menu = await Get("/api/connect/v1/menu", user, clientId);
        menu.Body.EnumerateArray().SelectMany(g => g.GetProperty("items").EnumerateArray()).Select(i => i.GetProperty("code").GetString())
            .ShouldBe([itemProjects, itemHelp], ignoreOrder: true, "reports is not given to the user; help opens no screen");
        var privileges = await Get("/api/connect/v1/privileges", user, clientId);
        privileges.Body.EnumerateObject().Select(p => p.Name).ShouldBe([budget, projects], ignoreOrder: true, "the other app's screen is not shown to this app");
        privileges.Body.GetProperty(projects).EnumerateArray().Select(a => a.GetString()).ShouldBe(["EDIT", "VIEW"]);

        (await Get($"/api/connect/v1/lovs/{ownList}/values", user, clientId)).Body.GetProperty("items")[0].GetProperty("value").GetString().ShouldBe("HIGH");
        (await Get($"/api/connect/v1/lovs/{entityList}/values", user, clientId)).Status.ShouldBe(HttpStatusCode.OK, "a list of the whole organization");
        (await Get($"/api/connect/v1/lovs/{otherList}/values", user, clientId)).Status.ShouldBe(HttpStatusCode.NotFound, "another app's list");
        (await Get($"/api/connect/v1/lovs/{ownList}/values?resource={staff}", user, clientId)).Status.ShouldBe(HttpStatusCode.NotFound, "another app's screen");
        (await Get($"/api/connect/v1/lovs/{ownList}/grant?resource={projects}", user, clientId)).Body.GetProperty("all").GetBoolean().ShouldBeTrue();

        // A company that does not use the app, or a user without one, gets no menu.
        (await Get("/api/connect/v1/menu", user, clientId, company: elsewhere)).Status.ShouldBe(HttpStatusCode.Forbidden, "the user works there, but the app does not");
        var outside = await Get("/api/connect/v1/me", outsider, clientId);
        outside.Body.GetProperty("hasAccess").GetBoolean().ShouldBeFalse();
        (await Get("/api/connect/v1/menu", outsider, clientId)).Status.ShouldBe(HttpStatusCode.Forbidden);

        // An inactive app is refused.
        await _harness.PublishAsync(AppsControlAreas.System, new Change(AppsControlChangeTypes.BusinessApp, other, ChangeOperation.Update, new
        {
            nameAr = "نظام",
            nameEn = "System",
            kind = "CONNECTED",
            companies = new[] { "*" },
            branches = new[] { "*" },
            isActive = false,
        }));
        (await Get("/api/connect/v1/app", null, "dc-app-" + other.ToLowerInvariant())).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Setting_up_sign_in_creates_the_client_with_the_audience_and_shows_each_secret_once()
    {
        var app = Harness.Unique("PMS");
        await _harness.PublishAsync(AppsControlAreas.System, App(app, new { baseUrl = "https://pms.example.gov.sa" }));
        var keycloak = new FakeKeycloakClients();
        var clients = new KeycloakSignInClients(new FakeClients(keycloak), Options.Create(new IdentityOptions { Provider = "Keycloak", BaseUrl = $"http://kc-{Guid.NewGuid():N}", Realm = "r" }), new FakeSecrets(), TimeProvider.System);

        var result = await _harness.AsAsync("admin", async sp =>
        {
            var service = new ConnectedAppService(sp.GetRequiredService<Persistence.AppsControlDbContext>(), clients, new ConnectedAppSettings(Audience), sp.GetRequiredService<ICurrentUser>(), NullLogger<ConnectedAppService>.Instance);
            var before = await service.GetAsync(app, CancellationToken.None);
            var created = await service.SetUpAsync(app, CancellationToken.None);
            var again = await service.SetUpAsync(app, CancellationToken.None);
            var replaced = await service.NewSecretAsync(app, CancellationToken.None);
            return (before, created, again, replaced);
        });

        result.before.Exists.ShouldBeFalse();
        result.created.Secret.ShouldBe("secret-1", "a new client's secret is shown once");
        result.created.SignIn.InSync.ShouldBeTrue();
        result.created.SignIn.RegisteredRedirectUris.ShouldBe(["https://pms.example.gov.sa/*"]);
        result.again.Secret.ShouldBeNull("updating the client keeps its secret");
        result.replaced.Secret.ShouldBe("secret-2");
        var client = keycloak.Clients.Values.Single();
        client["clientId"]!.GetValue<string>().ShouldBe("dc-app-" + app.ToLowerInvariant());
        client["publicClient"]!.GetValue<bool>().ShouldBeFalse();
        client["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeTrue();
        client["webOrigins"]!.AsArray().Select(o => o!.GetValue<string>()).ShouldBe(["https://pms.example.gov.sa"]);
        client["protocolMappers"]!.AsArray().Single()!["config"]!["included.custom.audience"]!.GetValue<string>().ShouldBe(Audience);

        await Should.ThrowAsync<AccountException>(() => _harness.AsAsync("admin", sp =>
            new ConnectedAppService(sp.GetRequiredService<Persistence.AppsControlDbContext>(), clients, new ConnectedAppSettings(Audience), sp.GetRequiredService<ICurrentUser>(), NullLogger<ConnectedAppService>.Instance)
                .SetUpAsync("CLASSIFICATION_" + Guid.NewGuid().ToString("N")[..6], CancellationToken.None)));
    }
}

/// <summary>Partner systems' sign-in clients for the public API (ADR-0042): service accounts only, with the public API's audience.</summary>
public sealed class PartnerClientTests
{
    [Fact]
    public async Task A_partner_client_gets_client_credentials_only_and_the_public_audience()
    {
        var keycloak = new FakeKeycloakClients();
        var clients = new PartnerClients(new KeycloakSignInClients(
            new FakeClients(keycloak),
            Options.Create(new IdentityOptions { Provider = "Keycloak", BaseUrl = $"http://kc-{Guid.NewGuid():N}", Realm = "r" }),
            new FakeSecrets(),
            TimeProvider.System));
        var audience = DynamicCube.Platform.PublicApi.PublicApiAuthorization.DefaultAudience;

        (await clients.FindAsync("dc-api-partner", audience, CancellationToken.None)).ShouldBeNull();
        (await clients.SaveAsync("dc-api-partner", "Partner (PARTNER)", audience, CancellationToken.None)).ShouldBeTrue();
        (await clients.SecretAsync("dc-api-partner", CancellationToken.None)).ShouldBe("secret-1");
        (await clients.SaveAsync("dc-api-partner", "Partner (PARTNER)", audience, CancellationToken.None)).ShouldBeFalse("updated, not created again");
        (await clients.NewSecretAsync("dc-api-partner", CancellationToken.None)).ShouldBe("secret-2");

        var client = keycloak.Clients.Values.Single();
        client["standardFlowEnabled"]!.GetValue<bool>().ShouldBeFalse("no one signs in with it");
        client["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeTrue();
        client["redirectUris"]!.AsArray().ShouldBeEmpty();
        client["protocolMappers"]!.AsArray().Single()!["config"]!["included.custom.audience"]!.GetValue<string>().ShouldBe(audience);
        var found = (await clients.FindAsync("dc-api-partner", audience, CancellationToken.None)).ShouldNotBeNull();
        (found.Enabled, found.ServiceAccount, found.HasAudience).ShouldBe((true, true, true));
        (await clients.TokenUrlAsync(CancellationToken.None)).ShouldBe("https://sso.example.gov.sa/realms/r/protocol/openid-connect/token");
    }
}

/// <summary>A test identity from headers: the user, the client the token was issued to (<c>azp</c>) and its audience.</summary>
internal sealed class TokenAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestToken";
    public const string UserHeader = "X-Test-User";
    public const string ClientHeader = "X-Test-Client";
    public const string AudienceHeader = "X-Test-Audience";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers[UserHeader].FirstOrDefault() is not { Length: > 0 } name)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("sub", "sub-" + name), new("preferred_username", name), new("name", name) };
        if (Request.Headers[ClientHeader].FirstOrDefault() is { Length: > 0 } client)
        {
            claims.Add(new Claim("azp", client));
        }

        if (Request.Headers[AudienceHeader].FirstOrDefault() is { Length: > 0 } audience)
        {
            claims.Add(new Claim("aud", audience));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, "preferred_username", "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>A small Keycloak admin API for clients: token, find, create, update, protocol mappers and secrets.</summary>
internal sealed class FakeKeycloakClients : HttpMessageHandler
{
    private int _secrets;

    public ConcurrentDictionary<string, JsonObject> Clients { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        if (path.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
        {
            return Json(new { access_token = "t", expires_in = 300 });
        }

        if (path.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal))
        {
            return Json(new { issuer = "https://sso.example.gov.sa/realms/r" });
        }

        if (request.Headers.Authorization?.Parameter != "t")
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        var parts = path[(path.IndexOf("/admin/realms/r/", StringComparison.Ordinal) + "/admin/realms/r/".Length)..].Split('/');
        switch (request.Method.Method, parts)
        {
            case ("GET", ["clients"]):
                var clientId = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["clientId"];
                return Json(Clients.Values.Where(c => (string)c["clientId"]! == clientId).ToList());
            case ("POST", ["clients"]):
                var created = JsonNode.Parse(body!)!.AsObject();
                created["id"] = Guid.NewGuid().ToString();
                created["secret"] = "secret-" + Interlocked.Increment(ref _secrets).ToString(CultureInfo.InvariantCulture);
                Clients[(string)created["id"]!] = created;
                return new HttpResponseMessage(HttpStatusCode.Created);
            case ("PUT", ["clients", var id]):
                foreach (var (key, value) in JsonNode.Parse(body!)!.AsObject())
                {
                    Clients[id][key] = value?.DeepClone();
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("POST", ["clients", var id, "protocol-mappers", "models"]):
                (Clients[id]["protocolMappers"] ??= new JsonArray()).AsArray().Add(JsonNode.Parse(body!));
                return new HttpResponseMessage(HttpStatusCode.Created);
            case ("GET", ["clients", var id, "client-secret"]):
                return Json(new { type = "secret", value = (string)Clients[id]["secret"]! });
            case ("POST", ["clients", var id, "client-secret"]):
                Clients[id]["secret"] = "secret-" + Interlocked.Increment(ref _secrets).ToString(CultureInfo.InvariantCulture);
                return Json(new { type = "secret", value = (string)Clients[id]["secret"]! });
            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static HttpResponseMessage Json(object value) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
}
