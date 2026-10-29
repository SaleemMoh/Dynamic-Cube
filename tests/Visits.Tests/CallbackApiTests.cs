extern alias ExternalApi;

using System.Net;
using System.Text;
using DynamicCube.Modules.Visits.Runtime;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Visits.Tests;

/// <summary>External inspection systems' result notifications as they arrive over HTTP: anonymous, signed over the raw body.</summary>
public sealed class CallbackApiTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<ExternalApi::Program> _factory = null!;

    public ValueTask InitializeAsync()
    {
        var connectionString = fixture.ConnectionString;
        _factory = new WebApplicationFactory<ExternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none");
            builder.UseSetting($"Secrets:{TestSecrets.CallbackSecret}:key", TestSecrets.CallbackKey);
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task A_signed_result_completes_the_visit_and_an_unsigned_one_is_refused()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new VisitHost(fixture);
        var type = await host.ExternalTypeAsync(polled: false);
        host.NextReference = "INSP-" + Guid.NewGuid().ToString("N")[..8];
        var visit = await host.RequestAsync(type, Guid.CreateVersion7());
        using var client = _factory.CreateClient();

        HttpRequestMessage Post(string body, string? signature)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"/api/visits/v1/callbacks/{type}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (signature is not null)
            {
                message.Headers.Add("X-Signature", signature);
            }

            return message;
        }

        var progress = $$"""{"inspectionId":"{{host.NextReference}}","status":"ASSIGNED"}""";
        var done = $$"""{"inspectionId":"{{host.NextReference}}","status":"COMPLETED","outcome":"COMPLIANT","findings":{"violations":0},"report":"All clear"}""";
        var unknown = $$"""{"inspectionId":"{{host.NextReference}}","status":"COMPLETED","outcome":"MAYBE"}""";
        (await client.SendAsync(Post(done, null), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(Post(done, ExternalVisits.Sign("another-key", done)), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(Post(progress, ExternalVisits.Sign(TestSecrets.CallbackKey, progress)), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.SendAsync(Post(unknown, ExternalVisits.Sign(TestSecrets.CallbackKey, unknown)), ct)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.SendAsync(Post(done, "sha256=" + ExternalVisits.Sign(TestSecrets.CallbackKey, done)), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await host.AsAsync("officer", sp => sp.GetRequiredService<VisitRegistry>().GetAsync(visit.Id, CancellationToken.None));
        detail.Visit.Status.ShouldBe("COMPLETED");
        detail.Visit.Outcome.ShouldBe("PASS");
        detail.Report.ShouldBe("All clear");
        detail.Events.Select(e => e.Type).ShouldContain("EXTERNAL_PROGRESS");
        detail.Events.Select(e => e.Type).ShouldContain("EXTERNAL_REFUSED");

        // A repeat is accepted without effect; unknown types and references are not found.
        (await client.SendAsync(Post(done, ExternalVisits.Sign(TestSecrets.CallbackKey, done)), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsync("/api/visits/v1/callbacks/NO_SUCH_TYPE", new StringContent("{}"), ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var stranger = """{"inspectionId":"NOT-OURS","status":"COMPLETED"}""";
        (await client.SendAsync(Post(stranger, ExternalVisits.Sign(TestSecrets.CallbackKey, stranger)), ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
