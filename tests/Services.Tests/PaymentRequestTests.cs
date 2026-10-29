using System.Text.Json.Nodes;
using DynamicCube.Modules.Payments.Changes;
using DynamicCube.Modules.Payments.Runtime;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>A workflow payment step: the request's fees are billed and processing waits until the bill is paid.</summary>
public sealed class PaymentRequestTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_request_waits_for_its_bill_continues_when_paid_and_closes_when_the_bill_is_not_paid()
    {
        await using var applicant = await Applicant.CreateAsync(fixture);
        var host = applicant.Host;
        var sample = new ServiceSamples();
        await host.PublishAsync(ServiceChangeTypes.Area, sample.All());
        var provider = TestHost.Unique("CASHIER");
        await host.PublishAsync(PaymentChangeTypes.Area, new Change(PaymentChangeTypes.Provider, provider, ChangeOperation.Create, new PaymentProviderPayload { NameAr = "الصندوق", NameEn = "Cashier", DueDays = 5 }));
        await host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Version, sample.Service + "/2", ChangeOperation.Create, new
        {
            nameAr = "الدفع أولاً",
            nameEn = "Pay first",
            definition = new
            {
                slaWorkingDays = 10,
                nodes = new object[]
                {
                    new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "PAY" },
                    new { code = "PAY", type = "PAYMENT", nameAr = "سداد الرسوم", nameEn = "Pay the fees", provider, next = "APPROVED", expired = "UNPAID" },
                    new { code = "APPROVED", type = "END", nameAr = "اكتمال", nameEn = "Completed", outcome = "APPROVED" },
                    new { code = "UNPAID", type = "END", nameAr = "لم تُسدد", nameEn = "Not paid", outcome = "CLOSED", displayStatus = "CLOSED_UNPAID" },
                },
            },
        }));

        async Task<RequestView> SubmitAsync(string level)
        {
            var draft = await applicant.AsAsync(sp => Requests(sp).CreateAsync(new CreateRequest(sample.Service), CancellationToken.None));
            var data = new JsonObject { [sample.Sectors] = new JsonArray("ICT"), [sample.IctCategory] = "PLATINUM", [sample.Employees] = 12, [sample.Revenue] = 60_000_000 };
            await applicant.AsAsync(sp => Requests(sp).SaveAsync(draft.Request.Id, new SaveRequest(data, level, "DETAILS"), CancellationToken.None));
            return await applicant.AsAsync(sp => Requests(sp).SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        }

        Task<BillDetail> Staff(Func<BillRegistry, Task<BillDetail>> action) => host.AsAsync("officer", sp => action(sp.GetRequiredService<BillRegistry>()));

        // Premium has a fee: the request waits for its bill.
        var premium = await SubmitAsync("PREMIUM");
        premium.Request.Status.ShouldBe(StatusCategory.PendingPayment);
        premium.Request.DisplayStatus.ShouldBe("AWAITING_PAYMENT");
        var bill = premium.Bills.ShouldNotBeNull().ShouldHaveSingleItem();
        bill.Status.ShouldBe("PENDING");
        bill.Total.ShouldBe(15000m);
        bill.Lines.Single().Code.ShouldBe("PREMIUM_FEE");

        // Paid: processing continues (here straight to approval) and the timeline shows it.
        await Staff(r => r.MarkPaidAsync(bill.Id, new MarkPaidRequest("TRF-1", null), CancellationToken.None));
        var paid = await applicant.AsAsync(sp => Requests(sp).GetAsync(premium.Request.Id, CancellationToken.None));
        paid.Request.Status.ShouldBe(StatusCategory.Approved);
        paid.Bills.ShouldNotBeNull().Single().Status.ShouldBe("PAID");

        // Nothing to pay: the payment step passes on.
        (await SubmitAsync("REGULAR")).Request.Status.ShouldBe(StatusCategory.Approved);

        // A bill that will not be paid (cancelled here, or expired) takes the step's "expired" way.
        var unpaid = await SubmitAsync("PREMIUM");
        await Staff(r => r.CancelAsync(unpaid.Bills!.Single().Id, new CancelBillRequest("Applicant withdrew"), CancellationToken.None));
        var closed = await applicant.AsAsync(sp => Requests(sp).GetAsync(unpaid.Request.Id, CancellationToken.None));
        closed.Request.Status.ShouldBe(StatusCategory.Closed);
        closed.Request.DisplayStatus.ShouldBe("CLOSED_UNPAID");
        closed.Bills.ShouldNotBeNull().Single().Status.ShouldBe("CANCELLED");
    }

    [Fact]
    public async Task A_payment_step_needs_somewhere_to_go_when_the_bill_is_not_paid()
    {
        await using var applicant = await Applicant.CreateAsync(fixture);
        var sample = new ServiceSamples();
        await applicant.Host.PublishAsync(ServiceChangeTypes.Area, sample.All());
        var errors = await Should.ThrowAsync<ChangeValidationException>(() => applicant.Host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Version, sample.Service + "/2", ChangeOperation.Create, new
        {
            nameAr = "س",
            nameEn = "S",
            definition = new
            {
                nodes = new object[]
                {
                    new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "PAY" },
                    new { code = "PAY", type = "PAYMENT", nameAr = "سداد", nameEn = "Pay", dueDays = 400, amount = new { no_such_op = 1 }, next = "END" },
                    new { code = "END", type = "END", nameAr = "نهاية", nameEn = "End", outcome = "APPROVED" },
                },
            },
        })));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".expired", StringComparison.Ordinal));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".dueDays", StringComparison.Ordinal));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".amount", StringComparison.Ordinal));
    }

    private static RequestService Requests(IServiceProvider services) => services.GetRequiredService<RequestService>();
}
