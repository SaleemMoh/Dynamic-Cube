extern alias ExternalApi;

using System.Net;
using System.Text;
using DynamicCube.Modules.Payments.Runtime;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Payments.Tests;

/// <summary>The providers' payment notifications as they arrive over HTTP: anonymous, signed over the raw body.</summary>
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
    public async Task A_signed_notification_pays_the_bill_and_an_unsigned_one_is_refused()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new PaymentHost(fixture);
        var sadad = Random.Shared.NextInt64(8_000_000_000, 8_999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var provider = await host.SadadAsync(sadad);
        var bill = await host.BillAsync(provider, Guid.CreateVersion7(), ("FEE", 1150m));
        using var client = _factory.CreateClient();
        var body = $$"""{"sadadNumber":"{{sadad}}","status":"PAID","amount":1150.00,"paymentReference":"BANK-1"}""";

        HttpRequestMessage Post(string? signature)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/v1/callbacks/{provider}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (signature is not null)
            {
                message.Headers.Add("X-Signature", signature);
            }

            return message;
        }

        (await client.SendAsync(Post(null), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(Post(PaymentCallbacks.Sign("another-key", body)), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(Post(PaymentCallbacks.Sign(TestSecrets.CallbackKey, body)), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The API has its own services: the bill itself shows the payment.
        var paid = await host.Host.AsAsync("staff", sp => sp.GetRequiredService<BillRegistry>().GetAsync(bill.Id, CancellationToken.None));
        paid.Bill.Status.ShouldBe("PAID");
        paid.Info.PaymentReference.ShouldBe("BANK-1");
        (await client.PostAsync("/api/payments/v1/callbacks/NO_SUCH_PROVIDER", new StringContent("{}"), ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
