extern alias InternalApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>The Action Center API as the internal system calls it (enum values in UPPER_SNAKE case).</summary>
public sealed class ApiTests(DatabaseFixture fixture) : IAsyncLifetime
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
    public async Task Inboxes_and_supervision_filter_by_box_and_deadline()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new WorkflowHost(fixture);
        var role = Unique("CLERK");
        var clerk = User("clerk");
        await host.Host.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new { nameAr = "موظف", nameEn = "Clerk", privileges = new[] { new { resourceCode = "action-center", actions = new[] { "VIEW" } } } }),
            new Change(AppsControlChangeTypes.User, clerk, ChangeOperation.Create, new { nameAr = "موظف", nameEn = "Clerk", roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } } }));
        var workflow = await host.WorkflowAsync([Start("REVIEW"), Human("REVIEW", Role(role), [Act("OK", "DONE")], sla: new { workingDays = 5 }), End("DONE", "APPROVED")]);
        var subject = await host.StartAsync(workflow);

        using var client = Client(clerk);
        var queue = await client.GetFromJsonAsync<JsonElement>("/api/workflow/v1/inbox?box=QUEUE", ct);
        queue.GetProperty("items").EnumerateArray().ShouldContain(t => t.GetProperty("subjectId").GetGuid() == subject);
        var mine = await client.GetFromJsonAsync<JsonElement>("/api/workflow/v1/inbox?box=MINE", ct);
        mine.GetProperty("items").EnumerateArray().ShouldNotContain(t => t.GetProperty("subjectId").GetGuid() == subject);
        (await client.GetAsync("/api/workflow/v1/inbox?box=SOMEWHERE", ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/workflow/v1/supervision", ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var admin = Client("admin");
        var onTrack = await admin.GetFromJsonAsync<JsonElement>("/api/workflow/v1/supervision?sla=ON_TRACK&unassigned=true", ct);
        var task = onTrack.GetProperty("items").EnumerateArray().Single(t => t.GetProperty("subjectId").GetGuid() == subject);
        task.GetProperty("sla").GetString().ShouldBe("ON_TRACK");
        var breached = await admin.GetFromJsonAsync<JsonElement>("/api/workflow/v1/supervision?sla=BREACHED", ct);
        breached.GetProperty("items").EnumerateArray().ShouldNotContain(t => t.GetProperty("subjectId").GetGuid() == subject);
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
