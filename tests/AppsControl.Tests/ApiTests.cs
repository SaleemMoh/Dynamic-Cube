extern alias InternalApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>End-to-end through the internal API host, with a header-based test identity instead of Keycloak tokens.</summary>
public sealed class ApiTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<InternalApi::Program> _factory = null!;

    public ValueTask InitializeAsync()
    {
        var connectionString = fixture.ConnectionString;
        _factory = new WebApplicationFactory<InternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none");
            builder.UseSetting("AppsControl:BootstrapUsers:admin:0", "SYSTEM_ADMIN");
            builder.UseSetting("AppsControl:BootstrapUsers:maker:0", "CONFIG_MAKER");
            builder.UseSetting("AppsControl:BootstrapUsers:checker:0", "CONFIG_CHECKER");
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null));
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Apps_control_endpoints_require_authentication()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/apps-control/v1/me", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Maker_and_checker_publish_a_lookup_through_the_api()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        var cls = Harness.Unique("CITY");
        using var maker = Client("maker");
        using var checker = Client("checker");

        var menu = await maker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/me/menu", ct);
        menu[0].GetProperty("code").GetString().ShouldBe("APPS_CONTROL");

        var created = await maker.PostAsJsonAsync("/api/apps-control/v1/change-sets", new { area = "apps.lookups", title = "Cities" }, ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("changeSet").GetProperty("id").GetString();

        var invalid = await maker.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/items", new
        {
            entityType = "apps.class",
            entityCode = cls,
            operation = "CREATE",
            payload = new { nameAr = "", nameEn = "City" },
        }, ct);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("errors").TryGetProperty("nameAr", out _).ShouldBeTrue();

        (await maker.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/items", new
        {
            entityType = "apps.class",
            entityCode = cls,
            operation = "CREATE",
            payload = new { nameAr = "مدينة", nameEn = "City" },
        }, ct)).EnsureSuccessStatusCode();
        (await maker.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/items", new
        {
            entityType = "apps.domain",
            entityCode = $"{cls}/RUH",
            operation = "CREATE",
            payload = new { nameAr = "الرياض", nameEn = "Riyadh" },
        }, ct)).EnsureSuccessStatusCode();

        (await maker.PostAsync(new Uri($"/api/apps-control/v1/change-sets/{id}/submit", UriKind.Relative), null, ct)).EnsureSuccessStatusCode();

        var selfApprove = await maker.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/approve", new { comment = "self" }, ct);
        selfApprove.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var approved = await checker.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/approve", new { comment = "ok" }, ct);
        approved.EnsureSuccessStatusCode();
        var detail = await approved.Content.ReadFromJsonAsync<JsonElement>(ct);
        detail.GetProperty("changeSet").GetProperty("status").GetString().ShouldBe("PUBLISHED");
        detail.GetProperty("changeSet").GetProperty("createdBy").GetString().ShouldBe("maker");

        var domains = await checker.GetFromJsonAsync<JsonElement>($"/api/apps-control/v1/classes/{cls}/domains", ct);
        domains.GetArrayLength().ShouldBe(1);
        domains[0].GetProperty("nameEn").GetString().ShouldBe("Riyadh");

        var row = await checker.GetFromJsonAsync<JsonElement>($"/api/apps-control/v1/admin/apps.domain/{cls}/RUH", ct);
        row.GetProperty("code").GetString().ShouldBe($"{cls}/RUH");
        row.GetProperty("owner").GetString().ShouldBe("ENTITY");

        var counts = await checker.GetFromJsonAsync<Dictionary<string, int>>("/api/apps-control/v1/admin/counts/apps.class", ct);
        counts![cls].ShouldBe(1);
        var roles = await checker.GetFromJsonAsync<Dictionary<string, int>>("/api/apps-control/v1/admin/counts/apps.role", ct);
        roles!["CONFIG_CHECKER"].ShouldBeGreaterThanOrEqualTo(1);
        (await checker.GetAsync("/api/apps-control/v1/admin/counts/apps.lov", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Change_sets_can_be_filtered_by_upper_case_status()
    {
        fixture.RequireConnectionString();
        using var maker = Client("maker");

        var ok = await maker.GetAsync(new Uri("/api/apps-control/v1/change-sets?status=DRAFT&mine=true", UriKind.Relative), TestContext.Current.CancellationToken);
        var bad = await maker.GetAsync(new Uri("/api/apps-control/v1/change-sets?status=NOPE", UriKind.Relative), TestContext.Current.CancellationToken);

        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        bad.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Concurrent_first_requests_create_one_account()
    {
        fixture.RequireConnectionString();
        var userName = Harness.Unique("burst").ToLowerInvariant();
        using var client = Client(userName);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            client.GetAsync(new Uri("/api/apps-control/v1/me", UriKind.Relative), TestContext.Current.CancellationToken)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Apply_endpoint_saves_no_approval_changes_at_once()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var maker = Client("maker");

        var types = await maker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/types", ct);
        types.EnumerateArray().Single(t => t.GetProperty("entityType").GetString() == "apps.ui-text").GetProperty("requiresApproval").GetBoolean().ShouldBeFalse();

        var saved = await maker.PostAsJsonAsync("/api/apps-control/v1/change-sets/apply", new
        {
            entityType = "apps.ui-text",
            entityCode = Harness.Unique("api.text").ToLowerInvariant(),
            operation = "CREATE",
            payload = new { nameAr = "نص", nameEn = "Text" },
        }, ct);
        saved.EnsureSuccessStatusCode();
        (await saved.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("changeSet").GetProperty("status").GetString().ShouldBe("PUBLISHED");

        var refused = await maker.PostAsJsonAsync("/api/apps-control/v1/change-sets/apply", new
        {
            entityType = "apps.class",
            entityCode = Harness.Unique("CLS"),
            operation = "CREATE",
            payload = new { nameAr = "أ", nameEn = "A" },
        }, ct);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Checker_cannot_draft_changes()
    {
        fixture.RequireConnectionString();
        using var checker = Client("checker");

        var response = await checker.PostAsJsonAsync("/api/apps-control/v1/change-sets", new { area = "apps.lookups", title = "x" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Privileges_endpoint_lists_actions_per_resource()
    {
        fixture.RequireConnectionString();
        using var admin = Client("admin");

        var privileges = await admin.GetFromJsonAsync<Dictionary<string, string[]>>("/api/apps-control/v1/me/privileges", TestContext.Current.CancellationToken);

        privileges!["apps-control.change-sets"].ShouldBe(["APPROVE", "CREATE", "VIEW"]);
    }

    [Fact]
    public async Task The_radiation_registry_sample_packages_import_and_publish()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var maker = Client("maker");
        using var checker = Client("checker");

        foreach (var file in Directory.GetFiles(RepositoryFile("deploy/sample-data/radiation-registry"), "*.json").Order(StringComparer.Ordinal))
        {
            using var content = new StringContent(await File.ReadAllTextAsync(file, ct), System.Text.Encoding.UTF8, "application/json");
            var imported = await maker.PostAsync(new Uri("/api/apps-control/v1/change-sets/import", UriKind.Relative), content, ct);
            if (!imported.IsSuccessStatusCode)
            {
                Assert.Fail($"{Path.GetFileName(file)}: {await imported.Content.ReadAsStringAsync(ct)}");
            }

            foreach (var draft in (await imported.Content.ReadFromJsonAsync<JsonElement>(ct)).EnumerateArray())
            {
                var id = draft.GetProperty("changeSet").GetProperty("id").GetString();
                (await maker.PostAsync(new Uri($"/api/apps-control/v1/change-sets/{id}/submit", UriKind.Relative), null, ct)).EnsureSuccessStatusCode();
                var approved = await checker.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/approve", new { comment = "sample data" }, ct);
                approved.EnsureSuccessStatusCode();
                (await approved.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("changeSet").GetProperty("status").GetString().ShouldBe("PUBLISHED", Path.GetFileName(file));
            }
        }

        (await checker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/classes/MANUFACTURER/domains", ct)).GetArrayLength().ShouldBe(1542);
        var unit = await checker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/apps.domain/ACTIVITY_UNIT/10010", ct);
        unit.GetProperty("attributes").GetProperty("BQ_FACTOR").GetDecimal().ShouldBe(37_000_000_000m);
        var facility = await checker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/apps.domain/FACILITY/00013ER", ct);
        facility.GetProperty("attributes").GetProperty("CITY").GetString().ShouldBe("YANBU");
        facility.GetProperty("attributes").GetProperty("OPERATIONAL").GetBoolean().ShouldBeFalse();
        var city = await checker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/apps.domain/CITY/YANBU", ct);
        city.GetProperty("parentCode").GetString().ShouldBe("3");

        var role = await checker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/apps.role/SourcesTeamLead", ct);
        role.GetProperty("privileges").EnumerateArray().Select(p => p.GetProperty("resourceCode").GetString()).ShouldBe(["action-center", "AdminSource", "facility-profile"], ignoreOrder: true);
        var user = await checker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/apps.user/saleem.a", ct);
        user.GetProperty("roles").GetArrayLength().ShouldBe(11);
    }

    private static string RepositoryFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DynamicCube.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), relative);
    }

    private HttpClient Client(string userName)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, userName);
        return client;
    }
}

/// <summary>Authenticates requests carrying <c>X-Test-User</c> as that user (subject <c>sub-&lt;name&gt;</c>).</summary>
internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var values) || values.Count == 0 || string.IsNullOrEmpty(values[0]))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var name = values[0]!;
        var identity = new ClaimsIdentity([new Claim("sub", "sub-" + name), new Claim("preferred_username", name), new Claim("name", name)], SchemeName, "preferred_username", "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
