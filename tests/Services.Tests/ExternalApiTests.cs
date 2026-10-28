extern alias ExternalApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>The portal API: catalog, draft, autosave, submission errors and success for an applicant acting for an establishment.</summary>
public sealed class ExternalApiTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<ExternalApi::Program> _factory = null!;

    public ValueTask InitializeAsync()
    {
        var connectionString = fixture.ConnectionString;
        _factory = new WebApplicationFactory<ExternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none");
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = ApplicantAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = ApplicantAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = ApplicantAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, ApplicantAuthHandler>(ApplicantAuthHandler.SchemeName, null));
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Applicant_fills_in_and_submits_a_request()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var applicant = await Applicant.CreateAsync(fixture);
        var sample = new ServiceSamples();
        await applicant.Host.PublishAsync(ServiceChangeTypes.Area, sample.All());
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApplicantAuthHandler.Header, applicant.Identity.NationalId);

        // Acting for themselves the establishment-only service is not available.
        var selfCatalog = await client.GetFromJsonAsync<JsonElement>("/api/services/v1/catalog", ct);
        Entry(selfCatalog, sample.Service).GetProperty("isEligible").GetBoolean().ShouldBeFalse();
        var refused = await client.PostAsJsonAsync("/api/services/v1/requests", new { serviceCode = sample.Service }, ct);
        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await refused.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("reasons")[0].GetProperty("code").GetString().ShouldBe("PARTY_KIND");

        client.DefaultRequestHeaders.Add(IActingPartyAccessor.Header, applicant.EstablishmentId.ToString());
        var service = await client.GetFromJsonAsync<JsonElement>($"/api/services/v1/services/{sample.Service}", ct);
        service.GetProperty("feesByLevel").GetProperty("PREMIUM")[0].GetProperty("isEstimate").GetBoolean().ShouldBeTrue();
        service.GetProperty("version").GetProperty("questions").GetProperty(sample.Sectors).GetProperty("type").GetString().ShouldBe("SELECT");

        var created = await client.PostAsJsonAsync("/api/services/v1/requests", new { serviceCode = sample.Service }, ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("request").GetProperty("id").GetString();

        var invalid = await client.PostAsJsonAsync($"/api/services/v1/requests/{id}/submit", new { declarationAccepted = true }, ct);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await invalid.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("errors");
        errors.EnumerateArray().Select(e => e.GetProperty("field").GetString()).ShouldContain(sample.Sectors);
        errors[0].GetProperty("messageAr").GetString().ShouldNotBeNullOrEmpty();

        var data = new Dictionary<string, object> { [sample.Sectors] = new[] { "ICT" }, [sample.IctCategory] = "PLATINUM", [sample.Employees] = 12, [sample.Revenue] = 60_000_000 };
        (await client.PutAsJsonAsync($"/api/services/v1/requests/{id}", new { data, levelCode = "PREMIUM", currentStep = "DETAILS" }, ct)).EnsureSuccessStatusCode();
        var submitted = await client.PostAsJsonAsync($"/api/services/v1/requests/{id}/submit", new { declarationAccepted = true }, ct);
        submitted.EnsureSuccessStatusCode();
        var view = await submitted.Content.ReadFromJsonAsync<JsonElement>(ct);
        view.GetProperty("request").GetProperty("status").GetString().ShouldBe("APPROVED", "the service's default workflow needs no review");
        view.GetProperty("request").GetProperty("feeTotal").GetDecimal().ShouldBe(15000m);

        var mine = await client.GetFromJsonAsync<JsonElement>("/api/services/v1/requests", ct);
        mine.EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldContain(id);
    }

    [Fact]
    public async Task Anyone_browses_the_portal_catalog_and_service_cards()
    {
        var connectionString = fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using (var host = new TestHost(connectionString))
        {
            await host.PublishAsync(ServiceChangeTypes.Area, [.. Packages.Items("portal-services.json").Where(i => i.EntityType.StartsWith("svc.", StringComparison.Ordinal))]);
        }

        // No sign-in: the categories, the listed services with duration and cost, and the statistics.
        using var client = _factory.CreateClient();
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/services/v1/portal/catalog", ct);
        var categories = catalog.GetProperty("categories").EnumerateArray().Select(c => c.GetProperty("code").GetString()).ToList();
        categories.ShouldContain("PERMITS");
        categories.IndexOf("PERMITS").ShouldBeLessThan(categories.IndexOf("CERTIFICATES"));
        var services = catalog.GetProperty("services");
        var permit = Entry(services, "EVENT_PERMIT");
        permit.GetProperty("categoryCode").GetString().ShouldBe("PERMITS");
        permit.GetProperty("duration").GetProperty("max").GetInt32().ShouldBe(5);
        permit.GetProperty("cost").GetProperty("min").GetDecimal().ShouldBe(500m);
        permit.GetProperty("cost").GetProperty("max").GetDecimal().ShouldBe(500m);
        permit.GetProperty("cost").GetProperty("dependsOnRequest").GetBoolean().ShouldBeFalse();
        var signboard = Entry(services, "SIGNBOARD_LICENSE").GetProperty("cost");
        (signboard.GetProperty("min").GetDecimal(), signboard.GetProperty("max").GetDecimal()).ShouldBe((200m, 1500m));
        signboard.GetProperty("dependsOnRequest").GetBoolean().ShouldBeTrue();
        Entry(services, "COMPLETION_CERTIFICATE").GetProperty("cost").GetProperty("max").GetDecimal().ShouldBe(0m);
        catalog.GetProperty("lastUpdated").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));

        var card = await client.GetFromJsonAsync<JsonElement>("/api/services/v1/portal/services/EVENT_PERMIT", ct);
        card.GetProperty("category").GetProperty("nameEn").GetString().ShouldBe("Permits");
        card.GetProperty("levels")[0].GetProperty("slaWorkingDays").GetInt32().ShouldBe(5);
        card.GetProperty("card").GetProperty("steps").GetArrayLength().ShouldBe(3);
        card.GetProperty("card").GetProperty("requiredDocuments")[1].GetProperty("noteEn").GetString().ShouldBe("For private sites");
        card.GetProperty("card").GetProperty("channels").EnumerateArray().Select(c => c.GetString()).ShouldBe(["PORTAL", "MOBILE_APP"]);
        (await client.GetAsync("/api/services/v1/portal/services/NO_SUCH_SERVICE", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var statistics = await client.GetFromJsonAsync<JsonElement>("/api/services/v1/portal/statistics", ct);
        statistics.GetProperty("services").GetInt32().ShouldBeGreaterThanOrEqualTo(4);

        // The applicant's own catalog still needs a sign-in.
        (await client.GetAsync("/api/services/v1/catalog", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static JsonElement Entry(JsonElement catalog, string code) => catalog.EnumerateArray().Single(e => e.GetProperty("code").GetString() == code);
}

/// <summary>Signs in an applicant whose national ID is given in a header.</summary>
internal sealed class ApplicantAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-National-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var values) || string.IsNullOrEmpty(values.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var id = values.ToString();
        var identity = new ClaimsIdentity([new Claim("sub", "sub-" + id), new Claim("preferred_username", id), new Claim("name", "مقدم الطلب"), new Claim("national_id", id)], SchemeName, "preferred_username", "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
