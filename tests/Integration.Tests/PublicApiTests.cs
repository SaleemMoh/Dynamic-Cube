extern alias ExternalApi;
extern alias InternalApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.PublicApi;
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

/// <summary>
/// The public API (ADR-0042): endpoints and API clients are checked when saved; partners call endpoints on the external host
/// with tokens that reach nothing else, see only their companies' data, are rate limited and logged.
/// </summary>
public sealed class PublicApiTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Audience = PublicApiAuthorization.DefaultAudience;

    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];
    private TestHost _host = null!;
    private WebApplicationFactory<ExternalApi::Program> _external = null!;
    private WebApplicationFactory<InternalApi::Program> _internal = null!;
    private string _class = null!;
    private string _companyA = null!;
    private string _companyB = null!;

    public async ValueTask InitializeAsync()
    {
        var connectionString = fixture.ConnectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none";
        _external = new WebApplicationFactory<ExternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString);
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = PublicTokenHandler.SchemeName;
                    options.DefaultAuthenticateScheme = PublicTokenHandler.SchemeName;
                    options.DefaultChallengeScheme = PublicTokenHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, PublicTokenHandler>(PublicTokenHandler.SchemeName, null));
        });
        _internal = new WebApplicationFactory<InternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString);
            builder.UseSetting("AppsControl:BootstrapUsers:admin:0", "SYSTEM_ADMIN");
            builder.UseSetting("PublicApi:BaseUrl", "https://api.example.gov.sa");
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null));
        });
        if (fixture.ConnectionString is null)
        {
            return;
        }

        _host = new TestHost(fixture.ConnectionString);
        (_class, _companyA, _companyB) = (TestHost.Unique("REGIONS"), TestHost.Unique("CO"), TestHost.Unique("CO"));
        await _host.PublishAsync(
            AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, _companyA, ChangeOperation.Create, new { nameAr = "أ", nameEn = "A" }),
            new Change(AppsControlChangeTypes.Company, _companyB, ChangeOperation.Create, new { nameAr = "ب", nameEn = "B" }));
        await _host.PublishAsync(
            AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Class, _class, ChangeOperation.Create, new { nameAr = "مناطق", nameEn = "Regions", valuesPerCompany = true, attributes = Array.Empty<object>() }));
        Change Value(string code, string[] companies) =>
            new(AppsControlChangeTypes.Domain, $"{_class}/{code}", ChangeOperation.Create, new { nameAr = "منطقة " + code, nameEn = "Region " + code, companies });
        await _host.PublishAsync(AppsControlAreas.Lookups, Value("R1", ["*"]), Value("R2", [_companyA]), Value("R3", [_companyB]));
    }

    public async ValueTask DisposeAsync()
    {
        await _external.DisposeAsync();
        await _internal.DisposeAsync();
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    private string Path(string rest) => $"/t-{_suffix}{rest}";

    private ApiEndpointPayload ClassEndpoint() => new()
    {
        NameAr = "المناطق",
        NameEn = "Regions",
        Path = Path("/regions"),
        Kind = ApiEndpointKind.Function,
        Function = "CLASS_VALUES",
        Parameters = [new ApiEndpointParameter { Name = "parent", Location = ApiParameterLocation.Query }],
        FunctionInputs = [new ApiFunctionBinding { Input = "class", Value = _class }, new ApiFunctionBinding { Input = "parent", Parameter = "parent" }],
    };

    private ApiEndpointPayload RowsEndpoint(string? path = null, bool single = false) => new()
    {
        NameAr = "القيم",
        NameEn = "Values",
        Path = path ?? Path("/values"),
        Kind = ApiEndpointKind.Query,
        Parameters = single
            ? [new ApiEndpointParameter { Name = "code", Location = ApiParameterLocation.Path, IsMasked = true }]
            : [new ApiEndpointParameter { Name = "prefix", Location = ApiParameterLocation.Query }],
        Query = JsonNode.Parse(JsonSerializer.Serialize(new
        {
            from = new { kind = "CLASS", code = _class, alias = "r" },
            where = new { match = "ALL", conditions = new[] { new { column = "r.code", op = single ? "EQ" : "STARTS_WITH", param = single ? "code" : "prefix" } } },
            sort = new[] { new { column = "r.code", direction = "ASC" } },
        })),
        Fields = [new ApiEndpointField { Name = "code", Column = "r.code" }, new ApiEndpointField { Name = "name", Column = "r.name_en" }],
        SingleRow = single,
        PageSize = 2,
    };

    private static ApiClientPayload Client(string clientId, string[] endpoints, string[] companies, int rate = 1000) => new()
    {
        NameAr = "شريك",
        NameEn = "Partner",
        ClientId = clientId,
        Endpoints = [.. endpoints],
        Companies = [.. companies],
        Branches = ["*"],
        RateLimitPerMinute = rate,
    };

    [Fact]
    public async Task Endpoints_and_clients_are_checked_when_saved()
    {
        fixture.RequireConnectionString();
        async Task<IReadOnlyCollection<string>> Problems(string type, object payload) =>
            (await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(IntegrationChangeTypes.Area,
                new Change(type, TestHost.Unique("E"), ChangeOperation.Create, payload)))).Errors.Keys.ToList();

        var badPath = ClassEndpoint();
        badPath.Path = "regions?x=1";
        (await Problems(IntegrationChangeTypes.ApiEndpoint, badPath)).ShouldContain("path");

        var placeholders = RowsEndpoint(Path("/values/{id}"));
        placeholders.Parameters.Add(new ApiEndpointParameter { Name = "limit", Location = ApiParameterLocation.Query });
        placeholders.Parameters.Add(new ApiEndpointParameter { Name = "ghost", Location = ApiParameterLocation.Path });
        (await Problems(IntegrationChangeTypes.ApiEndpoint, placeholders)).ShouldBe(["path", "parameters[1].name", "parameters[2].name"], ignoreOrder: true);

        var function = ClassEndpoint();
        function.FunctionInputs = [new ApiFunctionBinding { Input = "parent", Parameter = "nope" }, new ApiFunctionBinding { Input = "colour", Value = "red" }];
        (await Problems(IntegrationChangeTypes.ApiEndpoint, function)).ShouldBe(["functionInputs", "functionInputs[0].parameter", "functionInputs[1].input"], ignoreOrder: true);
        var unknown = ClassEndpoint();
        unknown.Function = "DROP_EVERYTHING";
        (await Problems(IntegrationChangeTypes.ApiEndpoint, unknown)).ShouldBe(["function"]);

        var query = RowsEndpoint();
        query.Query = JsonNode.Parse("""{"from":{"kind":"TABLE","code":"apps.class","alias":"r"}}""");
        query.Fields = [new ApiEndpointField { Name = "1st", Column = "r.code" }];
        query.PageSize = 5000;
        (await Problems(IntegrationChangeTypes.ApiEndpoint, query)).ShouldContain("query.from.code", "only registered tables and classes");
        (await Problems(IntegrationChangeTypes.ApiEndpoint, query)).ShouldContain("pageSize");

        // The same path shape is answered once; a used endpoint stays.
        var first = TestHost.Unique("E");
        await _host.PublishAsync(IntegrationChangeTypes.Area, new Change(IntegrationChangeTypes.ApiEndpoint, first, ChangeOperation.Create, RowsEndpoint(Path("/one/{code}"), single: true)));
        (await Problems(IntegrationChangeTypes.ApiEndpoint, RowsEndpoint(Path("/ONE/{other}"), single: true))).ShouldContain("path");

        (await Problems(IntegrationChangeTypes.ApiClient, Client("dc-api-x-" + _suffix, [first, "NO_SUCH"], []))).ShouldBe(["endpoints[1]", "companies"], ignoreOrder: true);
        (await Problems(IntegrationChangeTypes.ApiClient, Client("bad id!", [first], ["NOWHERE"]))).ShouldBe(["clientId", "companies[0]"], ignoreOrder: true);
        var client = TestHost.Unique("C");
        await _host.PublishAsync(IntegrationChangeTypes.Area, new Change(IntegrationChangeTypes.ApiClient, client, ChangeOperation.Create, Client("dc-api-y-" + _suffix, [first], ["*"])));
        (await Problems(IntegrationChangeTypes.ApiClient, Client("dc-api-y-" + _suffix, [first], ["*"]))).ShouldBe(["clientId"], "a sign-in client belongs to one API client");
        await Should.ThrowAsync<ChangeSetException>(() => _host.PublishAsync(IntegrationChangeTypes.Area, new Change(IntegrationChangeTypes.ApiEndpoint, first, ChangeOperation.Delete, null)));
    }

    [Fact]
    public async Task Partners_read_their_companies_data_with_tokens_that_reach_nothing_else()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        var (classEndpoint, rows, one) = (TestHost.Unique("REGIONS"), TestHost.Unique("VALUES"), TestHost.Unique("VALUE"));
        var (partner, limited) = (TestHost.Unique("PARTNER"), TestHost.Unique("LIMITED"));
        var (partnerId, limitedId) = ("dc-api-p-" + _suffix, "dc-api-l-" + _suffix);
        await _host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.ApiEndpoint, classEndpoint, ChangeOperation.Create, ClassEndpoint()),
            new Change(IntegrationChangeTypes.ApiEndpoint, rows, ChangeOperation.Create, RowsEndpoint()),
            new Change(IntegrationChangeTypes.ApiEndpoint, one, ChangeOperation.Create, RowsEndpoint(Path("/values/{code}"), single: true)),
            new Change(IntegrationChangeTypes.ApiClient, partner, ChangeOperation.Create, Client(partnerId, [classEndpoint, rows, one], [_companyA])),
            new Change(IntegrationChangeTypes.ApiClient, limited, ChangeOperation.Create, Client(limitedId, [classEndpoint], ["*"], rate: 2)));

        using var api = _external.CreateClient();
        async Task<(HttpStatusCode Status, JsonNode? Body, HttpResponseMessage Response)> Get(string path, string? client, string audience = Audience)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (client is not null)
            {
                request.Headers.Add(PublicTokenHandler.UserHeader, "service-account-" + client);
                request.Headers.Add(PublicTokenHandler.ClientHeader, client);
                request.Headers.Add(PublicTokenHandler.AudienceHeader, audience);
            }

            var response = await api.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            return (response.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text), response);
        }

        string Url(string rest) => "/api/public/v1" + Path(rest);
        static string[] Codes(JsonNode? list) => [.. list!.AsArray().Select(v => v!["code"]!.GetValue<string>())];

        // Class values for the client's company: shared ones and its own, never another company's.
        var regions = await Get(Url("/regions"), partnerId);
        regions.Status.ShouldBe(HttpStatusCode.OK);
        Codes(regions.Body).ShouldBe(["R1", "R2"], ignoreOrder: true);

        // Rows page by page, within the endpoint's page size.
        var page = await Get(Url("/values?prefix=R&limit=1"), partnerId);
        page.Status.ShouldBe(HttpStatusCode.OK);
        Codes(page.Body!["items"]).ShouldBe(["R1"]);
        page.Body!["hasMore"]!.GetValue<bool>().ShouldBeTrue();
        var next = await Get(Url("/values?prefix=R&limit=1&offset=1"), partnerId);
        Codes(next.Body!["items"]).ShouldBe(["R2"]);
        next.Body!["hasMore"]!.GetValue<bool>().ShouldBeFalse();
        var tooMany = await Get(Url("/values?limit=50"), partnerId);
        tooMany.Status.ShouldBe(HttpStatusCode.BadRequest);
        tooMany.Body!["errors"]!.AsObject().Select(e => e.Key).ShouldBe(["limit"]);

        // One row, or not found (another company's value is not found either).
        (await Get(Url("/values/R2"), partnerId)).Body!["name"]!.GetValue<string>().ShouldBe("Region R2");
        (await Get(Url("/values/R3"), partnerId)).Status.ShouldBe(HttpStatusCode.NotFound);

        // Who may call what.
        (await Get(Url("/regions"), null)).Status.ShouldBe(HttpStatusCode.Unauthorized);
        (await Get(Url("/regions"), "dc-api-unknown-" + _suffix)).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await Get(Url("/values"), limitedId)).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await Get(Url("/nothing-here"), partnerId)).Status.ShouldBe(HttpStatusCode.NotFound);
        (await Get(Url("/regions"), partnerId, audience: "dynamic-cube-external-api")).Status.ShouldBe(HttpStatusCode.Forbidden, "a portal token is not an API client's token");
        (await Get("/api/me", partnerId)).Status.ShouldBe(HttpStatusCode.Forbidden, "an API client's token reaches only the public API");

        // The rate limit: the third call in the minute waits.
        (await Get(Url("/regions"), limitedId)).Status.ShouldBe(HttpStatusCode.OK);
        (await Get(Url("/regions"), limitedId)).Status.ShouldBe(HttpStatusCode.OK);
        var limitedCall = await Get(Url("/regions"), limitedId);
        limitedCall.Status.ShouldBe(HttpStatusCode.TooManyRequests);
        limitedCall.Response.Headers.RetryAfter.ShouldNotBeNull();

        // The client's own description lists its endpoints.
        var description = await Get("/api/public/v1/openapi.json", partnerId);
        description.Status.ShouldBe(HttpStatusCode.OK);
        description.Body!["paths"]!.AsObject().Select(p => p.Key).ShouldBe([Path("/regions"), Path("/values"), Path("/values/{code}")], ignoreOrder: true);
        description.Body!["paths"]![Path("/values")]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["properties"]!["items"]!["items"]!["properties"]!["code"]!["type"]!.GetValue<string>().ShouldBe("string");

        // Every call is logged; masked parameters are not.
        using var admin = _internal.CreateClient();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.Header, "admin");
        var calls = await admin.GetFromJsonAsync<JsonElement>($"/api/integration/v1/public-api/calls?client={partner}&take=200", ct);
        var paths = calls.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("path").GetString()).ToList();
        paths.ShouldContain(Path("/values/***"));
        paths.ShouldNotContain(Path("/values/R2"));
        paths.ShouldContain(Path("/values?prefix=R&limit=1&offset=1"));
        calls.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("status").GetInt32()).ShouldContain(400);
        var refused = await admin.GetFromJsonAsync<JsonElement>($"/api/integration/v1/public-api/calls?status=4&endpoint={rows}", ct);
        refused.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(2, "the bad limit and the limited client");
    }

    [Fact]
    public async Task Administrators_try_unsaved_endpoints_list_functions_and_hand_out_descriptions()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var admin = _internal.CreateClient();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.Header, "admin");

        var functions = await admin.GetFromJsonAsync<JsonElement>("/api/integration/v1/public-api/functions", ct);
        functions.EnumerateArray().Select(f => f.GetProperty("code").GetString()).ShouldBe(["CLASS_VALUES", "DOCUMENT_VERIFY", "REQUEST_STATUS"]);

        var info = await admin.GetFromJsonAsync<JsonElement>("/api/integration/v1/public-api/info", ct);
        info.GetProperty("apiUrl").GetString().ShouldBe("https://api.example.gov.sa/api/public/v1");
        info.GetProperty("audience").GetString().ShouldBe(Audience);

        // As a partner of company B would get it, before it is saved.
        var client = TestHost.Unique("C");
        var endpoint = TestHost.Unique("E");
        await _host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.ApiEndpoint, endpoint, ChangeOperation.Create, ClassEndpoint()),
            new Change(IntegrationChangeTypes.ApiClient, client, ChangeOperation.Create, Client("dc-api-t-" + _suffix, [endpoint], [_companyB])));
        var tried = await admin.PostAsJsonAsync("/api/integration/v1/public-api/endpoints/try", new { endpoint = RowsEndpoint(), inputs = new { prefix = "R" }, client }, ct);
        tried.EnsureSuccessStatusCode();
        var result = await tried.Content.ReadFromJsonAsync<JsonElement>(ct);
        result.GetProperty("status").GetInt32().ShouldBe(200);
        result.GetProperty("body").GetProperty("items").EnumerateArray().Select(r => r.GetProperty("code").GetString()).ShouldBe(["R1", "R3"]);
        result.GetProperty("url").GetString().ShouldBe("/api/public/v1" + Path("/values?prefix=R&limit=2"));

        var broken = RowsEndpoint();
        broken.Fields = [];
        var invalid = await admin.PostAsJsonAsync("/api/integration/v1/public-api/endpoints/try", new { endpoint = broken, inputs = new { } }, ct);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("errors").EnumerateObject().Select(e => e.Name).ShouldContain("fields");

        var document = await admin.GetFromJsonAsync<JsonElement>($"/api/integration/v1/public-api/clients/{client}/openapi.json", ct);
        document.GetProperty("servers")[0].GetProperty("url").GetString().ShouldBe("https://api.example.gov.sa/api/public/v1");
        document.GetProperty("paths").EnumerateObject().Select(p => p.Name).ShouldBe([Path("/regions")]);
        document.GetProperty("paths").GetProperty(Path("/regions")).GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString().ShouldBe("array");

        var signIn = await admin.GetFromJsonAsync<JsonElement>($"/api/integration/v1/public-api/clients/{client}/sign-in", ct);
        signIn.GetProperty("managed").GetBoolean().ShouldBeFalse("the test realm is not managed from here");
        (await admin.PostAsync(new Uri($"/api/integration/v1/public-api/clients/{client}/sign-in", UriKind.Relative), null, ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.GetAsync(new Uri("/api/integration/v1/public-api/clients/NO_SUCH/sign-in", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

/// <summary>A test token from headers: the user, the client it was issued to (<c>azp</c>) and its audience.</summary>
internal sealed class PublicTokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PublicToken";
    public const string UserHeader = "X-Test-User";
    public const string ClientHeader = "X-Test-Client";
    public const string AudienceHeader = "X-Test-Audience";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers[UserHeader].FirstOrDefault() is not { Length: > 0 } name)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("sub", "sub-" + name), new("preferred_username", name) };
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
