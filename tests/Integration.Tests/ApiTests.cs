extern alias InternalApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Runtime;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Modules.Integration.Tests;

/// <summary>Integration configuration through the Apps Control change-set API, then the test console and call log.</summary>
public sealed class ApiTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly FakeProvider _provider = new();
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
            builder.UseSetting($"Secrets:{TestHost.SecretName}:clientId", "consumer-key");
            builder.UseSetting($"Secrets:{TestHost.SecretName}:clientSecret", "consumer-secret");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(IntegrationModule.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _provider);
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultScheme = TestAuthHandler.SchemeName;
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
            });
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _provider.Dispose();
    }

    [Fact]
    public async Task Maker_configures_an_operation_checker_approves_and_tests_it()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var maker = Client("maker");
        using var checker = Client("checker");
        var provider = TestHost.Unique("MCI");
        var connection = TestHost.Unique("MOMRAH");
        var envelope = TestHost.Unique("ENV");
        var operation = TestHost.Unique("MCI_CR_BY_ID");

        var menu = await maker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/me/menu", ct);
        menu.EnumerateArray().Select(g => g.GetProperty("code").GetString()).ShouldContain("INTEGRATION");

        var created = await maker.PostAsJsonAsync("/api/apps-control/v1/change-sets", new { area = IntegrationChangeTypes.Area, title = "MCI" }, ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("changeSet").GetProperty("id").GetString();
        await AddAsync(maker, id, IntegrationChangeTypes.Provider, provider, new ProviderPayload { NameAr = "وزارة التجارة", NameEn = "Ministry of Commerce" }, ct);
        await AddAsync(maker, id, IntegrationChangeTypes.Connection, connection, new ConnectionPayload
        {
            NameAr = "بوابة الوزارة",
            NameEn = "MoMRAH gateway",
            ProviderCode = provider,
            BaseUrl = "https://gateway.test",
            AuthType = Domain.AuthType.OAuth2ClientCredentials,
            SecretName = TestHost.SecretName,
            TokenUrl = "/oauth/v1/token",
        }, ct);
        await AddAsync(maker, id, IntegrationChangeTypes.EnvelopeProfile, envelope, MomrahSamples.Envelope(), ct);
        await AddAsync(maker, id, IntegrationChangeTypes.Operation, operation, MomrahSamples.CrByIdNumber(connection, envelope), ct);
        (await maker.PostAsync(new Uri($"/api/apps-control/v1/change-sets/{id}/submit", UriKind.Relative), null, ct)).EnsureSuccessStatusCode();

        var inbox = await checker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/change-sets?status=SUBMITTED", ct);
        inbox.EnumerateArray().Select(c => c.GetProperty("id").GetString()).ShouldContain(id);
        var approved = await checker.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/approve", new { comment = "ok" }, ct);
        approved.EnsureSuccessStatusCode();
        (await approved.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("changeSet").GetProperty("status").GetString().ShouldBe("PUBLISHED");

        var row = await maker.GetFromJsonAsync<JsonElement>($"/api/apps-control/v1/admin/{IntegrationChangeTypes.Operation}/{operation}", ct);
        row.GetProperty("connectionCode").GetString().ShouldBe(connection);
        row.GetProperty("isIdempotent").GetBoolean().ShouldBeTrue();

        _provider.Respond = r => r.Url.ToString() == "https://gateway.test/oauth/v1/token" ? (HttpStatusCode.OK, MomrahSamples.Token("t")) : (HttpStatusCode.OK, MomrahSamples.CrInformation);
        var tested = await maker.PostAsJsonAsync($"/api/integration/v1/operations/{operation}/test", new { inputs = new { idNumber = "1003721519" } }, ct);
        tested.EnsureSuccessStatusCode();
        var result = await tested.Content.ReadFromJsonAsync<JsonElement>(ct);
        result.GetProperty("outcome").GetString().ShouldBe("SUCCESS");
        result.GetProperty("outputs").GetProperty("crNumbers").GetArrayLength().ShouldBe(2);
        result.GetProperty("trace").GetProperty("httpStatus").GetInt32().ShouldBe(200);

        var missing = await maker.PostAsJsonAsync($"/api/integration/v1/operations/{operation}/test", new { inputs = new { } }, ct);
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var logs = await checker.GetFromJsonAsync<JsonElement>($"/api/integration/v1/call-logs?operation={operation}", ct);
        logs.GetProperty("total").GetInt32().ShouldBe(1);
        var callId = logs.GetProperty("items")[0].GetProperty("id").GetString();
        var log = await checker.GetFromJsonAsync<JsonElement>($"/api/integration/v1/call-logs/{callId}", ct);
        log.GetProperty("url").GetString().ShouldEndWith("/***");
    }

    [Fact]
    public async Task Checker_cannot_change_integrations_and_others_cannot_use_the_console()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var checker = Client("checker");
        using var stranger = Client("stranger");

        var draft = await checker.PostAsJsonAsync("/api/apps-control/v1/change-sets", new { area = IntegrationChangeTypes.Area, title = "x" }, ct);
        var test = await stranger.PostAsJsonAsync("/api/integration/v1/operations/ANY/test", new { inputs = new { } }, ct);
        var logs = await stranger.GetAsync(new Uri("/api/integration/v1/call-logs", UriKind.Relative), ct);

        draft.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        test.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        logs.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_mci_configuration_package_imports_and_publishes()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var maker = Client("maker");
        using var checker = Client("checker");
        var package = JsonDocument.Parse(await File.ReadAllTextAsync(RepositoryFile("deploy/config-packages/momrah-mci.json"), ct)).RootElement;

        var imported = await maker.PostAsJsonAsync("/api/apps-control/v1/change-sets/import", package, ct);
        if (!imported.IsSuccessStatusCode)
        {
            Assert.Fail(await imported.Content.ReadAsStringAsync(ct));
        }

        var drafts = await imported.Content.ReadFromJsonAsync<JsonElement>(ct);
        drafts.EnumerateArray().Select(d => d.GetProperty("changeSet").GetProperty("area").GetString()).ShouldBe(["integ.integrations", "party.registry"]);
        foreach (var draft in drafts.EnumerateArray())
        {
            var id = draft.GetProperty("changeSet").GetProperty("id").GetString();
            (await maker.PostAsync(new Uri($"/api/apps-control/v1/change-sets/{id}/submit", UriKind.Relative), null, ct)).EnsureSuccessStatusCode();
            var approved = await checker.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/approve", new { comment = "ok" }, ct);
            approved.EnsureSuccessStatusCode();
            (await approved.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("changeSet").GetProperty("status").GetString().ShouldBe("PUBLISHED");
        }

        var connection = await maker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/integ.connection/MOMRAH_GATEWAY", ct);
        connection.GetProperty("authType").GetString().ShouldBe("OAUTH2_CLIENT_CREDENTIALS");
        var source = await maker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/party.context-source/MCI_CRS", ct);
        source.GetProperty("operationCode").GetString().ShouldBe("MCI_CR_BY_ID_NUMBER");
        var fields = await maker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/admin/party.profile-field", ct);
        fields.GetArrayLength().ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task A_package_with_an_invalid_item_leaves_no_drafts()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var maker = Client("maker");
        var provider = TestHost.Unique("PKG");

        // Other tests create change sets as the same maker in parallel, so look for this package's title rather than counting.
        var title = $"Broken package {provider}";

        var response = await maker.PostAsJsonAsync("/api/apps-control/v1/change-sets/import", new
        {
            title,
            items = new object[]
            {
                new { entityType = IntegrationChangeTypes.Provider, entityCode = provider, operation = "CREATE", payload = new { nameAr = "جهة", nameEn = "Provider" } },
                new { entityType = IntegrationChangeTypes.Connection, entityCode = TestHost.Unique("C"), operation = "CREATE", payload = new { nameAr = "اتصال", nameEn = "Connection", providerCode = provider, baseUrl = "not a url" } },
            },
        }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var mine = await maker.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/change-sets?mine=true", ct);
        mine.EnumerateArray().ShouldNotContain(c => c.GetProperty("title").GetString() == title);
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

    private static async Task AddAsync(HttpClient client, string? id, string type, string code, object payload, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync($"/api/apps-control/v1/change-sets/{id}/items", new { entityType = type, entityCode = code, operation = "CREATE", payload }, ct);
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{type} {code}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
        }
    }

    private HttpClient Client(string user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, user);
        return client;
    }
}

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
