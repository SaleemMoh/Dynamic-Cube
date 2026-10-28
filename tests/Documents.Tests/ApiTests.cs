extern alias ExternalApi;
extern alias InternalApi;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Modules.Documents.Runtime;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Modules.Documents.Tests;

/// <summary>The registry API (internal host) and public verification (external host, no sign-in).</summary>
public sealed class ApiTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly FakePdf _pdf = new();
    private WebApplicationFactory<InternalApi::Program> _internal = null!;
    private WebApplicationFactory<ExternalApi::Program> _external = null!;

    public ValueTask InitializeAsync()
    {
        var connectionString = fixture.ConnectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none";
        _internal = new WebApplicationFactory<InternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString);
            builder.UseSetting("AppsControl:BootstrapUsers:admin:0", "SYSTEM_ADMIN");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPdfRenderer>(_pdf);
                services.AddSingleton<IPartyFacts>(new FakeParties());
                services
                    .AddAuthentication(o => o.DefaultScheme = o.DefaultAuthenticateScheme = o.DefaultChallengeScheme = TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
            });
        });
        _external = new WebApplicationFactory<ExternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString);
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _internal.DisposeAsync();
        await _external.DisposeAsync();
    }

    [Fact]
    public async Task Officers_find_print_and_suspend_documents_and_anyone_can_verify_them()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new DocumentHost(fixture);
        var type = await host.TypeAsync();
        var issued = await host.IssueAsync(type, Guid.CreateVersion7(), "Riyadh", ("CONSTRUCTION", 2));
        var code = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<DocumentsDbContext>().Documents.Where(d => d.Id == issued.Id).Select(d => d.VerificationCode).SingleAsync());

        using var admin = _internal.CreateClient();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.Header, "admin");
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/documents/v1/documents?search={issued.Number}&status=ACTIVE", ct);
        page.GetProperty("total").GetInt32().ShouldBe(1);
        page.GetProperty("items")[0].GetProperty("status").GetString().ShouldBe("ACTIVE");

        using var pdf = await admin.GetAsync($"/api/documents/v1/documents/{issued.Id}/versions/1/pdf", ct);
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await pdf.Content.ReadAsStringAsync(ct)).ShouldStartWith("%PDF");

        var bad = await admin.PostAsJsonAsync($"/api/documents/v1/documents/{issued.Id}/suspend", new { comment = "" }, ct);
        bad.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var suspended = await admin.PostAsJsonAsync($"/api/documents/v1/documents/{issued.Id}/suspend", new { comment = "Inspection pending" }, ct);
        suspended.EnsureSuccessStatusCode();
        (await suspended.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("document").GetProperty("status").GetString().ShouldBe("SUSPENDED");

        (await admin.GetAsync("/api/documents/v1/documents?status=NOPE", ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var stranger = _internal.CreateClient();
        stranger.DefaultRequestHeaders.Add(TestAuthHandler.Header, "nobody");
        (await stranger.GetAsync("/api/documents/v1/documents", ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var anonymous = _external.CreateClient();
        var verify = await anonymous.GetFromJsonAsync<JsonElement>($"/api/documents/v1/verify/{code.ToLowerInvariant()}", ct);
        verify.GetProperty("number").GetString().ShouldBe(issued.Number);
        verify.GetProperty("status").GetString().ShouldBe("SUSPENDED");
        verify.GetProperty("lines")[0].GetProperty("grade").GetInt32().ShouldBe(2);
        (await anonymous.GetAsync("/api/documents/v1/verify/NOSUCHCODE", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync("/api/documents/v1/my", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
