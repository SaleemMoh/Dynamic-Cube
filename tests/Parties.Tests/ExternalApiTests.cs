extern alias ExternalApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DynamicCube.Modules.Integration;
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

namespace DynamicCube.Modules.Parties.Tests;

/// <summary>The portal API: contexts of the signed-in applicant (national ID claim) and the acting-party header.</summary>
public sealed class ExternalApiTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<ExternalApi::Program> _factory = null!;
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        var connectionString = fixture.ConnectionString;
        _host = PartyTestSupport.Host(fixture, new TestApplicant());
        _factory = new WebApplicationFactory<ExternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none");
            builder.UseSetting($"Secrets:{TestHost.SecretName}:clientId", "consumer-key");
            builder.UseSetting($"Secrets:{TestHost.SecretName}:clientSecret", "consumer-secret");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(IntegrationModule.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _host.Provider);
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultScheme = ApplicantAuthHandler.SchemeName;
                        options.DefaultAuthenticateScheme = ApplicantAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = ApplicantAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, ApplicantAuthHandler>(ApplicantAuthHandler.SchemeName, null);
            });
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task Applicant_lists_contexts_and_acts_only_for_them()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        var prefix = await PartyTestSupport.PublishMciContextSourceAsync(_host);
        var cr = PartyTestSupport.CrNumber();
        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, PartyTestSupport.Licenses((cr, "مؤسسة البوابة", "1", "Active"))));
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApplicantAuthHandler.Header, PartyTestSupport.NationalId());

        var contexts = await client.GetFromJsonAsync<JsonElement>("/api/party/v1/me/contexts?refresh=true", ct);
        var establishment = contexts.GetProperty("contexts")[0];
        establishment.GetProperty("identifier").GetString().ShouldBe(cr);
        establishment.GetProperty("relationshipCode").GetString().ShouldBe("OWNER");
        establishment.GetProperty("kind").GetString().ShouldBe("ESTABLISHMENT");

        using var acting = new HttpRequestMessage(HttpMethod.Get, "/api/party/v1/me/acting");
        acting.Headers.Add(IActingPartyAccessor.Header, establishment.GetProperty("partyId").GetString());
        var actingParty = await (await client.SendAsync(acting, ct)).Content.ReadFromJsonAsync<JsonElement>(ct);
        actingParty.GetProperty("nameAr").GetString().ShouldBe("مؤسسة البوابة");
        actingParty.GetProperty("isSelf").GetBoolean().ShouldBeFalse();

        using var forged = new HttpRequestMessage(HttpMethod.Get, "/api/party/v1/me/acting");
        forged.Headers.Add(IActingPartyAccessor.Header, Guid.NewGuid().ToString());
        (await client.SendAsync(forged, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Registry_endpoints_are_not_on_the_portal_api()
    {
        fixture.RequireConnectionString();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApplicantAuthHandler.Header, PartyTestSupport.NationalId());

        var response = await client.GetAsync(new Uri("/api/party/v1/parties", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
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
