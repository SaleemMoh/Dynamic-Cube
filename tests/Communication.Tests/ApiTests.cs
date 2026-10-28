extern alias InternalApi;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Modules.Communication.Tests;

/// <summary>The message log, event catalog and an employee's notifications on the internal host.</summary>
public sealed class ApiTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<InternalApi::Program> _internal = null!;

    public ValueTask InitializeAsync()
    {
        var connectionString = fixture.ConnectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none";
        _internal = new WebApplicationFactory<InternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString);
            builder.UseSetting("AppsControl:BootstrapUsers:admin:0", "SYSTEM_ADMIN");
            builder.ConfigureTestServices(services => services
                .AddAuthentication(o => o.DefaultScheme = o.DefaultAuthenticateScheme = o.DefaultChallengeScheme = TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null));
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _internal.DisposeAsync();

    [Fact]
    public async Task Officers_read_the_log_and_employees_their_notifications()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new CommunicationHost(fixture);
        var employee = TestHost.Unique("emp").ToLowerInvariant();
        await host.RoleWithUsersAsync((employee, "emp@example.com"));
        var template = await host.TemplateAsync(sms: false);
        await host.RuleAsync(template, ["IN_APP", "EMAIL"], [new { kind = "USER", value = employee }]);
        var raised = await host.RaiseAsync(Guid.CreateVersion7());

        using var admin = Client("admin");
        var events = await admin.GetFromJsonAsync<JsonElement>("/api/communication/v1/events", ct);
        events.EnumerateArray().Select(e => e.GetProperty("code").GetString()).ShouldContain("REQUEST_RETURNED");
        var log = await admin.GetFromJsonAsync<JsonElement>($"/api/communication/v1/messages?search={raised.SubjectNumber}&status=pending", ct);
        log.GetProperty("total").GetInt32().ShouldBe(1);
        var email = log.GetProperty("items")[0];
        email.GetProperty("channel").GetString().ShouldBe("EMAIL");
        email.GetProperty("status").GetString().ShouldBe("PENDING");
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/communication/v1/messages/{email.GetProperty("id").GetGuid()}", ct);
        detail.GetProperty("body").GetString()!.ShouldContain("dir=\"rtl\"");
        (await admin.PostAsync($"/api/communication/v1/messages/{email.GetProperty("id").GetGuid()}/resend", null, ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.GetAsync("/api/communication/v1/messages?status=LOST", ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var me = Client(employee);
        (await me.GetAsync("/api/communication/v1/messages", ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var mine = await me.GetFromJsonAsync<JsonElement>("/api/communication/v1/notifications", ct);
        mine.GetProperty("unread").GetInt32().ShouldBe(1);
        mine.GetProperty("items")[0].GetProperty("titleEn").GetString().ShouldBe("Request approved");
        (await me.PostAsync("/api/communication/v1/notifications/read-all", null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await me.GetFromJsonAsync<JsonElement>("/api/communication/v1/notifications/unread-count", ct)).GetProperty("count").GetInt32().ShouldBe(0);
    }

    private HttpClient Client(string user)
    {
        var client = _internal.CreateClient();
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
