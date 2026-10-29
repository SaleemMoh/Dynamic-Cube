using System.Collections.Concurrent;
using System.Net;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Payments.Changes;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Security;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Payments.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The gateway credentials of the test host plus the callback key of the test providers.</summary>
internal sealed class TestSecrets : ISecretStore
{
    public const string CallbackSecret = "PAY_CALLBACK";
    public const string CallbackKey = "callback-signing-key";

    public string? Find(string name, string key) => (name, key) switch
    {
        (TestHost.SecretName, "clientId") => "consumer-key",
        (TestHost.SecretName, "clientSecret") => "consumer-secret",
        (CallbackSecret, "key") => CallbackKey,
        _ => null,
    };
}

/// <summary>Remembers the bills closed (paid, expired, cancelled), as the workflow would be told.</summary>
internal sealed class RecordingListener : IPaymentListener
{
    public ConcurrentQueue<BillClosed> Closed { get; } = new();

    public Task OnBillClosedAsync(BillClosed bill, CancellationToken cancellationToken)
    {
        Closed.Enqueue(bill);
        return Task.CompletedTask;
    }
}

internal sealed class PaymentHost : IAsyncDisposable
{
    public PaymentHost(DatabaseFixture fixture)
    {
        Host = new TestHost(fixture.RequireConnectionString(), s =>
        {
            s.AddSingleton<ISecretStore, TestSecrets>();
            s.AddSingleton(Clock);
            s.AddSingleton<TimeProvider>(Clock);
            s.AddSingleton<IPaymentListener>(Listener);
        });
    }

    public TestHost Host { get; }

    public TestClock Clock { get; } = new();

    public RecordingListener Listener { get; } = new();

    /// <summary>A SADAD-like provider: bills are registered through an operation answering with the SADAD number.</summary>
    public async Task<string> SadadAsync(string sadadNumber)
    {
        Host.Provider.Respond = request => request.Url.AbsolutePath switch
        {
            "/oauth/v1/token" => (HttpStatusCode.OK, MomrahSamples.Token("token-1")),
            _ => (HttpStatusCode.OK, $$"""{ "statusDetails": [{ "code": 200, "message": "Ok" }], "data": { "responseCode": "1", "responseMessage": "success", "result": { "sadadNumber": "{{sadadNumber}}", "billerCode": "123" } } }"""),
        };
        var operation = await MomrahSamples.PublishAsync(Host, operation: (connection, envelope) => new OperationPayload
        {
            NameAr = "إنشاء فاتورة سداد",
            NameEn = "Create SADAD bill",
            ConnectionCode = connection,
            EnvelopeProfileCode = envelope,
            Method = "POST",
            PathTemplate = "/v1/SADAD/bills",
            IsIdempotent = false,
            Parameters =
            [
                Body("billNumber"), Body("amount"), Body("dueDate"), Body("payerId"), Body("payerName", masked: true), Body("description"),
            ],
            Outputs = [new() { Name = "billNumber", Path = "sadadNumber" }],
        });
        var provider = TestHost.Unique("SADAD");
        await Host.PublishAsync(PaymentChangeTypes.Area, new Change(PaymentChangeTypes.Provider, provider, ChangeOperation.Create, new PaymentProviderPayload
        {
            NameAr = "سداد",
            NameEn = "SADAD",
            CreateOperation = operation,
            BillerCode = "123",
            DueDays = 3,
            PayUrlTemplate = "https://pay.test/sadad/{billNumber}?amount={amount}",
            InstructionsAr = "ادفع عبر سداد برمز المفوتر 123.",
            InstructionsEn = "Pay through SADAD with biller code 123.",
            CallbackSecret = TestSecrets.CallbackSecret,
            Callback = new() { BillNumber = "sadadNumber" },
        }));
        return provider;
    }

    /// <summary>A provider without an operation: bills are numbered locally and staff record payments.</summary>
    public async Task<string> ManualAsync()
    {
        var provider = TestHost.Unique("CASHIER");
        await Host.PublishAsync(PaymentChangeTypes.Area, new Change(PaymentChangeTypes.Provider, provider, ChangeOperation.Create, new PaymentProviderPayload
        {
            NameAr = "الصندوق",
            NameEn = "Cashier",
            DueDays = 1,
        }));
        return provider;
    }

    public Task<BillInfo> BillAsync(string provider, Guid subject, params (string Code, decimal Total)[] lines) =>
        Host.AsAsync("staff", sp => sp.GetRequiredService<IPayments>().CreateBillAsync(
            new BillRequest(provider, "TEST", subject, "T-1", Guid.CreateVersion7(), "مؤسسة الاختبار", "رسوم الطلب", "Request fees",
                [.. lines.Select(l => new BillLine(l.Code, "رسم " + l.Code, "Fee " + l.Code, Math.Round(l.Total / 1.15m, 2), l.Total - Math.Round(l.Total / 1.15m, 2), l.Total))]),
            CancellationToken.None));

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    private static OperationParameter Body(string name, bool masked = false) => new() { Name = name, Location = ParameterLocation.Body, IsMasked = masked };
}
