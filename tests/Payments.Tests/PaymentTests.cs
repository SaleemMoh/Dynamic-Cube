using System.Text.Json.Nodes;
using DynamicCube.Modules.Payments.Changes;
using DynamicCube.Modules.Payments.Domain;
using DynamicCube.Modules.Payments.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Payments;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Payments.Tests;

public sealed class PaymentTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Bills_are_registered_with_the_provider_and_paid_by_a_signed_notification()
    {
        await using var host = new PaymentHost(fixture);
        var sadad = Random.Shared.NextInt64(9_000_000_000, 9_999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var provider = await host.SadadAsync(sadad);
        var subject = Guid.CreateVersion7();

        var bill = await host.BillAsync(provider, subject, ("PREMIUM", 5000m), ("SERVICE", 750m));
        bill.ProviderBillNumber.ShouldBe(sadad);
        bill.Total.ShouldBe(5750m);
        bill.Status.ShouldBe("PENDING");
        bill.BillerCode.ShouldBe("123");
        bill.PayUrl.ShouldBe($"https://pay.test/sadad/{sadad}?amount=5750.00");
        bill.DueAt.ShouldBe(host.Clock.Now.AddDays(3), TimeSpan.FromMilliseconds(1));
        var sent = JsonNode.Parse(host.Host.Provider.Requests.Last(r => r.Url.AbsolutePath == "/v1/SADAD/bills").Body!)!;
        sent["amount"]!.GetValue<string>().ShouldBe("5750.00");
        sent["billNumber"]!.GetValue<string>().ShouldBe(bill.Number);

        Task<CallbackOutcome> NotifyAsync(string body, string? signature = null) =>
            host.Host.AsAsync("anonymous", sp => sp.GetRequiredService<PaymentCallbacks>().HandleAsync(provider, body, signature ?? PaymentCallbacks.Sign(TestSecrets.CallbackKey, body), CancellationToken.None));
        string Body(string status, decimal amount) => $$"""{"sadadNumber":"{{sadad}}","status":"{{status}}","amount":"{{amount}}","paymentReference":"REF-{{sadad}}","paidAt":"2026-11-02T10:00:00Z"}""";

        (await NotifyAsync(Body("PAID", 5750m), "sha256=" + new string('0', 64))).ShouldBe(CallbackOutcome.Unauthorized);
        (await NotifyAsync(Body("PAID", 5000m))).ShouldBe(CallbackOutcome.Refused);
        (await NotifyAsync(Body("PENDING", 5750m))).ShouldBe(CallbackOutcome.Accepted);
        (await NotifyAsync("""{"sadadNumber":"0000000000","status":"PAID","amount":"1"}""")).ShouldBe(CallbackOutcome.NotFound);
        host.Listener.Closed.ShouldBeEmpty();

        (await NotifyAsync(Body("PAID", 5750m))).ShouldBe(CallbackOutcome.Accepted);
        (await NotifyAsync(Body("PAID", 5750m))).ShouldBe(CallbackOutcome.Accepted, "a repeated notification changes nothing");
        var closed = host.Listener.Closed.ShouldHaveSingleItem();
        closed.Paid.ShouldBeTrue();
        closed.SubjectId.ShouldBe(subject);
        closed.PaymentReference.ShouldBe($"REF-{sadad}");

        var bills = await host.Host.AsAsync("staff", sp => sp.GetRequiredService<IPayments>().OfSubjectAsync("TEST", subject, CancellationToken.None));
        var paid = bills.ShouldHaveSingleItem();
        paid.Status.ShouldBe("PAID");
        paid.PayUrl.ShouldBeNull();
        paid.PaidAt.ShouldBe(DateTimeOffset.Parse("2026-11-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var detail = await host.Host.AsAsync("staff", sp => sp.GetRequiredService<BillRegistry>().GetAsync(bill.Id, CancellationToken.None));
        detail.Events.Select(e => e.Type).ShouldBe(["ISSUED", "NOTIFICATION_REFUSED", "NOTIFICATION_IGNORED", "PAID"]);
        (await host.Host.RaisedAsync(bill.Id)).Select(e => e.Code).ShouldBe(["BILL_ISSUED", "BILL_PAID"]);
    }

    [Fact]
    public async Task Staff_record_payments_and_cancel_bills_and_unpaid_bills_expire()
    {
        await using var host = new PaymentHost(fixture);
        // Far in the past: the expiry job runs for the whole database.
        host.Clock.Now = new DateTimeOffset(2020, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var provider = await host.ManualAsync();
        var registry = (Func<BillRegistry, Task<BillDetail>> action) => host.Host.AsAsync("officer", sp => action(sp.GetRequiredService<BillRegistry>()));

        var transfer = await host.BillAsync(provider, Guid.CreateVersion7(), ("FEE", 100m));
        transfer.ProviderBillNumber.ShouldBe(transfer.Number, "without an operation the platform's number is paid with");
        await Should.ThrowAsync<BillValidationException>(() => registry(r => r.MarkPaidAsync(transfer.Id, new MarkPaidRequest(null, null), CancellationToken.None)));
        var marked = await registry(r => r.MarkPaidAsync(transfer.Id, new MarkPaidRequest("TRF-778", "Bank transfer"), CancellationToken.None));
        marked.Bill.Status.ShouldBe("PAID");
        marked.Info.PaymentReference.ShouldBe("TRF-778");
        await Should.ThrowAsync<BillConflictException>(() => registry(r => r.CancelAsync(transfer.Id, new CancelBillRequest("late"), CancellationToken.None)));

        var cancelled = await host.BillAsync(provider, Guid.CreateVersion7(), ("FEE", 100m));
        (await registry(r => r.CancelAsync(cancelled.Id, new CancelBillRequest("Wrong amount"), CancellationToken.None))).Bill.Status.ShouldBe("CANCELLED");

        var unpaid = await host.BillAsync(provider, Guid.CreateVersion7(), ("FEE", 100m));
        host.Clock.Now = host.Clock.Now.AddDays(2);
        (await host.Host.AsAsync("worker", sp => sp.GetRequiredService<BillRegistry>().ExpireAsync(CancellationToken.None))).ShouldBeGreaterThanOrEqualTo(1);
        (await registry(r => r.GetAsync(unpaid.Id, CancellationToken.None))).Bill.Status.ShouldBe("EXPIRED");

        host.Listener.Closed.Select(c => (c.BillId, c.Paid, c.Status)).ShouldBe([(transfer.Id, true, "PAID"), (cancelled.Id, false, "CANCELLED"), (unpaid.Id, false, "EXPIRED")]);
    }

    [Fact]
    public async Task Providers_are_checked_and_bills_need_a_provider_and_an_amount()
    {
        await using var host = new PaymentHost(fixture);
        var errors = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(PaymentChangeTypes.Area,
            new Change(PaymentChangeTypes.Provider, TestHost.Unique("BAD"), ChangeOperation.Create, new PaymentProviderPayload
            {
                NameAr = "س",
                NameEn = "S",
                DueDays = 0,
                PayUrlTemplate = "javascript:alert(1)",
                CallbackSecret = "lower case",
                Callback = new CallbackFields { Amount = "a b c" },
            })));
        errors.Errors.Keys.Order().ShouldBe(["callback.amount", "callbackSecret", "dueDays", "payUrlTemplate"]);

        var provider = await host.ManualAsync();
        await Should.ThrowAsync<PaymentException>(() => host.BillAsync(provider, Guid.CreateVersion7()));
        await Should.ThrowAsync<PaymentException>(() => host.BillAsync("NO_SUCH_PROVIDER", Guid.CreateVersion7(), ("FEE", 10m)));
    }
}
