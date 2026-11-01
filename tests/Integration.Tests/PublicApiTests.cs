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
/// with tokens that reach nothing else, as their service account — whose roles give the endpoints, companies, branches and
/// data — or as a registered person on endpoints that allow it; calls are rate limited and logged.
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

    private static ApiClientPayload Client(string clientId, int rate = 1000, string? account = null) => new()
    {
        NameAr = "شريك",
        NameEn = "Partner",
        ClientId = clientId,
        Account = account,
        RateLimitPerMinute = rate,
    };

    /// <summary>The service account an API client acts as unless another is named.</summary>
    private static string AccountOf(string client) => "svc-" + client.ToLowerInvariant().Replace('_', '-');

    /// <summary>Gives the client's service account a role of its own that opens the endpoints, held where given.</summary>
    private Task<Guid> GiveAsync(string client, string[] endpoints, params (string Company, string[] Branches)[] holdings)
    {
        var role = TestHost.Unique("API");
        return _host.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new
            {
                nameAr = "واجهة",
                nameEn = "API",
                privileges = endpoints.Select(e => new { resourceCode = ApiResources.Of(e), actions = new[] { "VIEW" } }).ToArray(),
            }),
            new Change(AppsControlChangeTypes.User, AccountOf(client), ChangeOperation.Update, new
            {
                nameAr = "شريك",
                nameEn = "Partner",
                kind = "SERVICE",
                roles = holdings.Select(h => new { roleCode = role, companyCode = h.Company, branches = h.Branches }).ToArray(),
            }));
    }

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

        // A client acts as a service account: its own (created with it), or a named one — never a person's, never another client's.
        var person = "p." + _suffix;
        await _host.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, person, ChangeOperation.Create, new { nameAr = "شخص", nameEn = "Person" }));
        (await Problems(IntegrationChangeTypes.ApiClient, Client("bad id!"))).ShouldBe(["clientId"]);
        (await Problems(IntegrationChangeTypes.ApiClient, Client("dc-api-x-" + _suffix, account: person))).ShouldBe(["account"], "a person's account");
        (await Problems(IntegrationChangeTypes.ApiClient, Client("dc-api-x-" + _suffix, account: "svc-nobody-" + _suffix))).ShouldBe(["account"], "no such service account");
        var client = TestHost.Unique("C");
        await _host.PublishAsync(IntegrationChangeTypes.Area, new Change(IntegrationChangeTypes.ApiClient, client, ChangeOperation.Create, Client("dc-api-y-" + _suffix)));
        var account = await _host.AsAsync("admin", sp => sp.GetRequiredService<DynamicCube.Platform.Security.IServiceAccounts>().FindAsync(AccountOf(client), CancellationToken.None));
        account.ShouldNotBeNull("the client's own service account is created with it");
        account.NameEn.ShouldBe("Partner");
        (await Problems(IntegrationChangeTypes.ApiClient, Client("dc-api-y-" + _suffix))).ShouldBe(["clientId"], "a sign-in client belongs to one API client");
        (await Problems(IntegrationChangeTypes.ApiClient, Client("dc-api-z-" + _suffix, account: AccountOf(client)))).ShouldBe(["account"], "a service account belongs to one API client");

        // A service account never gets a sign-in account, and stays a service account.
        (await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.User, AccountOf(client), ChangeOperation.Update, new { nameAr = "شريك", nameEn = "Partner", kind = "PERSON" })))).Errors.Keys.ShouldBe(["kind"]);

        // An endpoint is a resource roles give; deleting it retires the resource.
        await _host.PublishAsync(IntegrationChangeTypes.Area, new Change(IntegrationChangeTypes.ApiEndpoint, first, ChangeOperation.Delete, null));
    }

    [Fact]
    public async Task Partners_read_their_companies_data_with_tokens_that_reach_nothing_else()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        var (classEndpoint, rows, one) = (TestHost.Unique("REGIONS"), TestHost.Unique("VALUES"), TestHost.Unique("VALUE"));
        var (partner, limited, wide) = (TestHost.Unique("PARTNER"), TestHost.Unique("LIMITED"), TestHost.Unique("WIDE"));
        var (partnerId, limitedId, wideId) = ("dc-api-p-" + _suffix, "dc-api-l-" + _suffix, "dc-api-w-" + _suffix);
        await _host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.ApiEndpoint, classEndpoint, ChangeOperation.Create, ClassEndpoint()),
            new Change(IntegrationChangeTypes.ApiEndpoint, rows, ChangeOperation.Create, RowsEndpoint()),
            new Change(IntegrationChangeTypes.ApiEndpoint, one, ChangeOperation.Create, RowsEndpoint(Path("/values/{code}"), single: true)),
            new Change(IntegrationChangeTypes.ApiClient, partner, ChangeOperation.Create, Client(partnerId)),
            new Change(IntegrationChangeTypes.ApiClient, limited, ChangeOperation.Create, Client(limitedId, rate: 2)),
            new Change(IntegrationChangeTypes.ApiClient, wide, ChangeOperation.Create, Client(wideId)));
        await GiveAsync(partner, [classEndpoint, rows, one], (_companyA, ["*"]));
        await GiveAsync(limited, [classEndpoint], (_companyB, ["*"]));

        using var api = _external.CreateClient();
        async Task<(HttpStatusCode Status, JsonNode? Body, HttpResponseMessage Response)> Get(string path, string? client, string audience = Audience, string? company = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (client is not null)
            {
                request.Headers.Add(PublicTokenHandler.UserHeader, "service-account-" + client);
                request.Headers.Add(PublicTokenHandler.ClientHeader, client);
                request.Headers.Add(PublicTokenHandler.AudienceHeader, audience);
            }

            if (company is not null)
            {
                request.Headers.Add(ApiHeaders.Company, company);
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

        // Who may call what: the endpoints the service account's roles give, in the companies they are held in.
        (await Get(Url("/regions"), null)).Status.ShouldBe(HttpStatusCode.Unauthorized);
        (await Get(Url("/regions"), "dc-api-unknown-" + _suffix)).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await Get(Url("/values"), limitedId)).Status.ShouldBe(HttpStatusCode.Forbidden, "its roles do not give it this endpoint");
        (await Get(Url("/regions"), wideId)).Status.ShouldBe(HttpStatusCode.Forbidden, "its service account holds no role yet");
        (await Get(Url("/regions"), partnerId, company: _companyB)).Status.ShouldBe(HttpStatusCode.Forbidden, "it does not work in company B");

        // Held in every company: the call names the company it works in.
        await GiveAsync(wide, [classEndpoint], ("*", ["*"]));
        var unnamed = await Get(Url("/regions"), wideId);
        unnamed.Status.ShouldBe(HttpStatusCode.BadRequest);
        unnamed.Body!["detail"]!.GetValue<string>().ShouldContain(ApiHeaders.Company);
        Codes((await Get(Url("/regions"), wideId, company: _companyB)).Body).ShouldBe(["R1", "R3"], ignoreOrder: true);
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
    public async Task Get_all_or_get_some_with_optional_inputs_left_empty()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        var (classes, some, client) = (TestHost.Unique("CLASSES"), TestHost.Unique("SOME"), TestHost.Unique("PARTNER"));
        var clientId = "dc-api-c-" + _suffix;

        // Optional inputs left empty are not given; a required one left empty is refused.
        var allClasses = new ApiEndpointPayload
        {
            NameAr = "التصنيفات",
            NameEn = "Classes",
            Path = Path("/classes"),
            Kind = ApiEndpointKind.Function,
            Function = "CLASSES",
            Parameters = [new ApiEndpointParameter { Name = "codes", Location = ApiParameterLocation.Query }],
            FunctionInputs = [new ApiFunctionBinding { Input = "classes", Parameter = "codes" }, new ApiFunctionBinding { Input = "values" }],
        };
        var someValues = ClassEndpoint();
        someValues.Path = Path("/regions/some");
        someValues.Parameters.Add(new ApiEndpointParameter { Name = "codes", Location = ApiParameterLocation.Query });
        someValues.FunctionInputs = [new ApiFunctionBinding { Input = "class", Value = _class }, new ApiFunctionBinding { Input = "parent", Value = " " }, new ApiFunctionBinding { Input = "codes", Parameter = "codes" }];
        var noClass = ClassEndpoint();
        noClass.FunctionInputs = [new ApiFunctionBinding { Input = "class" }];
        (await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.ApiEndpoint, TestHost.Unique("E"), ChangeOperation.Create, noClass)))).Errors.Keys.ShouldBe(["functionInputs"]);
        await _host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.ApiEndpoint, classes, ChangeOperation.Create, allClasses),
            new Change(IntegrationChangeTypes.ApiEndpoint, some, ChangeOperation.Create, someValues),
            new Change(IntegrationChangeTypes.ApiClient, client, ChangeOperation.Create, Client(clientId)));
        await GiveAsync(client, [classes, some], (_companyA, ["*"]));

        using var api = _external.CreateClient();
        async Task<(HttpStatusCode Status, JsonNode? Body)> Get(string rest)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/v1" + Path(rest));
            request.Headers.Add(PublicTokenHandler.UserHeader, "service-account-" + clientId);
            request.Headers.Add(PublicTokenHandler.ClientHeader, clientId);
            request.Headers.Add(PublicTokenHandler.AudienceHeader, Audience);
            var response = await api.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            return (response.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
        }

        static string[] Codes(JsonNode? list) => [.. list!.AsArray().Select(v => v!["code"]!.GetValue<string>())];

        // Get all: every active class, each with the values serving the client's company.
        var all = await Get("/classes");
        all.Status.ShouldBe(HttpStatusCode.OK);
        Codes(all.Body).ShouldContain(_class);
        Codes(all.Body).Length.ShouldBeGreaterThan(1);
        Codes(all.Body!.AsArray().Single(c => c!["code"]!.GetValue<string>() == _class)!["values"]).ShouldBe(["R1", "R2"], ignoreOrder: true);

        // Get some: only the classes asked for, in the order asked (commas or the parameter repeated).
        var one = await Get($"/classes?codes={_class}");
        Codes(one.Body).ShouldBe([_class]);
        Codes((await Get($"/classes?codes={_class}&codes={_class}")).Body).ShouldBe([_class], "the parameter repeated reads as a list, each class once");
        (await Get("/classes?codes=NO_SUCH_CLASS")).Status.ShouldBe(HttpStatusCode.BadRequest);

        // A class's values: all, or only the codes asked for.
        Codes((await Get("/regions/some")).Body).ShouldBe(["R1", "R2"], ignoreOrder: true);
        Codes((await Get("/regions/some?codes=R2")).Body).ShouldBe(["R2"]);
        Codes((await Get("/regions/some?codes=R1,R3")).Body).ShouldBe(["R1"], "R3 serves another company");
    }

    [Fact]
    public async Task Clients_act_for_registered_people_on_endpoints_that_allow_it()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        var (regions, plain, client, list, role) = (TestHost.Unique("REGIONS"), TestHost.Unique("PLAIN"), TestHost.Unique("PARTNER"), TestHost.Unique("LIST"), TestHost.Unique("R2_ONLY"));
        var clientId = "dc-api-a-" + _suffix;
        var (person, away, gone) = ("p1." + _suffix, "p2." + _suffix, "p3." + _suffix);

        var acting = ClassEndpoint();
        acting.Path = Path("/people/regions");
        acting.ActsForUser = true;
        var notActing = ClassEndpoint();
        notActing.Path = Path("/plain/regions");
        await _host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.ApiEndpoint, regions, ChangeOperation.Create, acting),
            new Change(IntegrationChangeTypes.ApiEndpoint, plain, ChangeOperation.Create, notActing),
            new Change(IntegrationChangeTypes.ApiClient, client, ChangeOperation.Create, Client(clientId)));
        await GiveAsync(client, [regions, plain], (_companyA, ["*"]));

        // A list of the class limits who sees which values: a person whose role is given R2 only.
        await _host.PublishAsync(AppsControlAreas.Lovs, new Change(AppsControlChangeTypes.Lov, list, ChangeOperation.Create, new { nameAr = "قائمة", nameEn = "List", sourceType = "CLASS", classCode = _class }));
        await _host.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new { nameAr = "دور", nameEn = "Role", grants = new[] { new { lovCode = list, grantType = "VALUES", grantedValues = new[] { "R2" } } } }),
            new Change(AppsControlChangeTypes.User, person, ChangeOperation.Create, new { nameAr = "شخص", nameEn = "Person", email = $"Person.{_suffix}@Example.com", roles = new[] { new { roleCode = role, companyCode = _companyA, branches = new[] { "*" } } } }),
            new Change(AppsControlChangeTypes.User, away, ChangeOperation.Create, new { nameAr = "شخص", nameEn = "Away", roles = new[] { new { roleCode = role, companyCode = _companyB, branches = new[] { "*" } } } }),
            new Change(AppsControlChangeTypes.User, gone, ChangeOperation.Create, new { nameAr = "شخص", nameEn = "Gone", status = "DISABLED", roles = new[] { new { roleCode = role, companyCode = _companyA, branches = new[] { "*" } } } }));

        using var api = _external.CreateClient();
        async Task<(HttpStatusCode Status, JsonNode? Body)> Get(string rest, string? actingUser)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/v1" + Path(rest));
            request.Headers.Add(PublicTokenHandler.UserHeader, "service-account-" + clientId);
            request.Headers.Add(PublicTokenHandler.ClientHeader, clientId);
            request.Headers.Add(PublicTokenHandler.AudienceHeader, Audience);
            if (actingUser is not null)
            {
                request.Headers.Add(ApiHeaders.ActingUser, actingUser);
            }

            var response = await api.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            return (response.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
        }

        static string[] Codes(JsonNode? list) => [.. list!.AsArray().Select(v => v!["code"]!.GetValue<string>())];

        // The service account: no grant on the list, so every value of its company.
        Codes((await Get("/people/regions", null)).Body).ShouldBe(["R1", "R2"], ignoreOrder: true);

        // Acting for the person (by username or email): their roles and data apply.
        Codes((await Get("/people/regions", person)).Body).ShouldBe(["R2"]);
        Codes((await Get("/people/regions", $"person.{_suffix}@example.com")).Body).ShouldBe(["R2"], "the email matches whatever its case");

        // Someone not registered here: the service account's own access.
        Codes((await Get("/people/regions", "nobody-" + _suffix)).Body).ShouldBe(["R1", "R2"], ignoreOrder: true);

        // Refused: an inactive person, a person who does not work in the call's company, an endpoint that acts for nobody.
        (await Get("/people/regions", gone)).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await Get("/people/regions", away)).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await Get("/plain/regions", person)).Status.ShouldBe(HttpStatusCode.BadRequest);

        // The call log tells whom the client acted for.
        using var admin = _internal.CreateClient();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.Header, "admin");
        var calls = await admin.GetFromJsonAsync<JsonElement>($"/api/integration/v1/public-api/calls?client={client}&take=50", ct);
        calls.GetProperty("items").EnumerateArray()
            .Where(c => c.GetProperty("actingFor").ValueKind == JsonValueKind.String)
            .Select(c => c.GetProperty("actingFor").GetString())
            .Distinct()
            .ShouldBe([person]);
    }

    [Fact]
    public async Task Administrators_try_unsaved_endpoints_list_functions_and_hand_out_descriptions()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var admin = _internal.CreateClient();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.Header, "admin");

        var functions = await admin.GetFromJsonAsync<JsonElement>("/api/integration/v1/public-api/functions", ct);
        functions.EnumerateArray().Select(f => f.GetProperty("code").GetString()).ShouldBe(["CLASSES", "CLASS_VALUES", "DOCUMENT_VERIFY", "LOOKUPS", "REQUEST_STATUS"]);

        var info = await admin.GetFromJsonAsync<JsonElement>("/api/integration/v1/public-api/info", ct);
        info.GetProperty("apiUrl").GetString().ShouldBe("https://api.example.gov.sa/api/public/v1");
        info.GetProperty("audience").GetString().ShouldBe(Audience);

        // As a partner of company B would get it (its service account's companies and data), before it is saved.
        var client = TestHost.Unique("C");
        var endpoint = TestHost.Unique("E");
        await _host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.ApiEndpoint, endpoint, ChangeOperation.Create, ClassEndpoint()),
            new Change(IntegrationChangeTypes.ApiClient, client, ChangeOperation.Create, Client("dc-api-t-" + _suffix)));
        await GiveAsync(client, [endpoint], (_companyB, ["*"]));
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
