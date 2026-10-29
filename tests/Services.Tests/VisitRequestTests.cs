using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Modules.Visits.Changes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Runtime;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>A workflow visit step: a visit is requested for the request and its outcome decides the way on.</summary>
public sealed class VisitRequestTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_request_waits_for_its_visit_and_the_outcome_or_a_cancellation_decides_the_way_on()
    {
        await using var applicant = await Applicant.CreateAsync(fixture);
        var host = applicant.Host;
        var sample = new ServiceSamples();
        await host.PublishAsync(ServiceChangeTypes.Area, sample.All());
        var type = await VisitTypeAsync(host);
        await host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Version, sample.Service + "/2", ChangeOperation.Create, new
        {
            nameAr = "المعاينة أولاً",
            nameEn = "Inspect first",
            definition = new
            {
                slaWorkingDays = 10,
                nodes = new object[]
                {
                    new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "SITE" },
                    new
                    {
                        code = "SITE",
                        type = "VISIT",
                        nameAr = "معاينة الموقع",
                        nameEn = "Site inspection",
                        visitType = type,
                        site = new { cat = new object[] { "الرياض — ", new { var = "data." + sample.IctCategory } } },
                        location = "24.7136,46.6753",
                        instructions = "Check the premises.",
                        dueDays = 3,
                        outcomes = new Dictionary<string, string> { ["FAIL"] = "REJECTED" },
                        next = "CHECK",
                        cancelled = "CLOSED",
                    },
                    new
                    {
                        code = "CHECK",
                        type = "GATEWAY",
                        nameAr = "الدرجة",
                        nameEn = "Score",
                        routes = new object[] { new { when = new Dictionary<string, object> { ["<"] = new object[] { new { var = "visits.SITE.data.score" }, 50 } }, to = "REJECTED" } },
                        @default = "APPROVED",
                    },
                    new { code = "APPROVED", type = "END", nameAr = "اكتمال", nameEn = "Completed", outcome = "APPROVED" },
                    new { code = "REJECTED", type = "END", nameAr = "رفض", nameEn = "Rejected", outcome = "REJECTED" },
                    new { code = "CLOSED", type = "END", nameAr = "إغلاق", nameEn = "Closed", outcome = "CLOSED", displayStatus = "CLOSED_NO_VISIT" },
                },
            },
        }));

        async Task<RequestView> SubmitAsync()
        {
            var draft = await applicant.AsAsync(sp => Requests(sp).CreateAsync(new CreateRequest(sample.Service), CancellationToken.None));
            var data = new JsonObject { [sample.Sectors] = new JsonArray("ICT"), [sample.IctCategory] = "PLATINUM", [sample.Employees] = 12, [sample.Revenue] = 60_000_000 };
            await applicant.AsAsync(sp => Requests(sp).SaveAsync(draft.Request.Id, new SaveRequest(data, "REGULAR", "DETAILS"), CancellationToken.None));
            return await applicant.AsAsync(sp => Requests(sp).SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        }

        Task<VisitDetail> Staff(Func<VisitRegistry, Task<VisitDetail>> action) => host.AsAsync("officer", sp => action(sp.GetRequiredService<VisitRegistry>()));
        Task<RequestView> ReloadAsync(RequestView request) => applicant.AsAsync(sp => Requests(sp).GetAsync(request.Request.Id, CancellationToken.None));

        // The request waits for its visit, which carries where to go and what to check.
        var passed = await SubmitAsync();
        passed.Request.Status.ShouldBe(StatusCategory.InProgress);
        passed.Request.DisplayStatus.ShouldBe("AWAITING_VISIT");
        var visit = passed.Visits.ShouldNotBeNull().ShouldHaveSingleItem();
        visit.Status.ShouldBe("PENDING");
        visit.SiteAddress.ShouldBe("الرياض — PLATINUM");
        var detail = await Staff(r => r.GetAsync(visit.Id, CancellationToken.None));
        detail.Latitude.ShouldBe(24.7136);
        detail.Instructions.ShouldBe("Check the premises.");
        detail.Visit.SubjectNumber.ShouldBe(passed.Request.Number);

        // Passed with a good score: the gateway reads the recorded data and approves.
        await Staff(r => r.RecordAsync(visit.Id, new RecordOutcomeRequest("PASS", new JsonObject { ["score"] = 80 }, null, null), CancellationToken.None));
        var approved = await ReloadAsync(passed);
        approved.Request.Status.ShouldBe(StatusCategory.Approved);
        approved.Visits.ShouldNotBeNull().Single().Outcome.ShouldBe("PASS");

        // Passed with a poor score: rejected by the gateway; failed: rejected by the outcome's own way.
        var poor = await SubmitAsync();
        await Staff(r => r.RecordAsync(poor.Visits!.Single().Id, new RecordOutcomeRequest("PASS", new JsonObject { ["score"] = 30 }, null, null), CancellationToken.None));
        (await ReloadAsync(poor)).Request.Status.ShouldBe(StatusCategory.Rejected);
        var failed = await SubmitAsync();
        await Staff(r => r.RecordAsync(failed.Visits!.Single().Id, new RecordOutcomeRequest("FAIL", [], null, null), CancellationToken.None));
        (await ReloadAsync(failed)).Request.Status.ShouldBe(StatusCategory.Rejected);

        // A cancelled visit takes the step's cancelled way.
        var cancelled = await SubmitAsync();
        await Staff(r => r.CancelAsync(cancelled.Visits!.Single().Id, new CancelVisitRequest("Site closed for good"), CancellationToken.None));
        var closed = await ReloadAsync(cancelled);
        closed.Request.Status.ShouldBe(StatusCategory.Closed);
        closed.Request.DisplayStatus.ShouldBe("CLOSED_NO_VISIT");
    }

    [Fact]
    public async Task A_visit_branch_no_longer_needed_after_a_join_has_its_visit_cancelled()
    {
        await using var applicant = await Applicant.CreateAsync(fixture);
        var host = applicant.Host;
        var sample = new ServiceSamples();
        await host.PublishAsync(ServiceChangeTypes.Area, sample.All());
        var type = await VisitTypeAsync(host);
        await host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Version, sample.Service + "/2", ChangeOperation.Create, new
        {
            nameAr = "أي معاينة",
            nameEn = "Any inspection",
            definition = new
            {
                nodes = new object[]
                {
                    new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "SPLIT" },
                    new { code = "SPLIT", type = "PARALLEL_SPLIT", nameAr = "تفرع", nameEn = "Split", branches = new[] { "FIRST", "SECOND" } },
                    new { code = "FIRST", type = "VISIT", nameAr = "الأولى", nameEn = "First", visitType = type, next = "JOIN", cancelled = "JOIN" },
                    new { code = "SECOND", type = "VISIT", nameAr = "الثانية", nameEn = "Second", visitType = type, next = "JOIN", cancelled = "JOIN" },
                    new { code = "JOIN", type = "JOIN", nameAr = "دمج", nameEn = "Join", mode = "ANY", next = "END" },
                    new { code = "END", type = "END", nameAr = "نهاية", nameEn = "End", outcome = "APPROVED" },
                },
            },
        }));

        var draft = await applicant.AsAsync(sp => Requests(sp).CreateAsync(new CreateRequest(sample.Service), CancellationToken.None));
        var data = new JsonObject { [sample.Sectors] = new JsonArray("ICT"), [sample.IctCategory] = "PLATINUM", [sample.Employees] = 12, [sample.Revenue] = 60_000_000 };
        await applicant.AsAsync(sp => Requests(sp).SaveAsync(draft.Request.Id, new SaveRequest(data, "REGULAR", "DETAILS"), CancellationToken.None));
        var request = await applicant.AsAsync(sp => Requests(sp).SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        request.Visits.ShouldNotBeNull().Count.ShouldBe(2);

        await host.AsAsync("officer", sp => sp.GetRequiredService<VisitRegistry>().RecordAsync(request.Visits[0].Id, new RecordOutcomeRequest("PASS", [], null, null), CancellationToken.None));
        var done = await applicant.AsAsync(sp => Requests(sp).GetAsync(request.Request.Id, CancellationToken.None));
        done.Request.Status.ShouldBe(StatusCategory.Approved);
        done.Visits.ShouldNotBeNull().Select(v => v.Status).ShouldBe(["COMPLETED", "CANCELLED"]);
    }

    [Fact]
    public async Task A_visit_step_needs_a_visit_type_and_somewhere_to_go_when_cancelled()
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
                    new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "SITE" },
                    new { code = "SITE", type = "VISIT", nameAr = "معاينة", nameEn = "Visit", dueDays = 400, site = new { no_such_op = 1 }, outcomes = new Dictionary<string, string> { ["PASS"] = "NOWHERE" }, next = "END" },
                    new { code = "END", type = "END", nameAr = "نهاية", nameEn = "End", outcome = "APPROVED" },
                },
            },
        })));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".visitType", StringComparison.Ordinal));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".cancelled", StringComparison.Ordinal));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".dueDays", StringComparison.Ordinal));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".site", StringComparison.Ordinal));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".outcomes.PASS", StringComparison.Ordinal));
    }

    private static async Task<string> VisitTypeAsync(TestHost host)
    {
        var type = TestHost.Unique("SITE");
        await host.PublishAsync(VisitChangeTypes.Area, new Change(VisitChangeTypes.Type, type, ChangeOperation.Create, new VisitTypePayload
        {
            NameAr = "معاينة الموقع",
            NameEn = "Site inspection",
            Mode = VisitMode.Field,
            Outcomes =
            [
                new() { Code = "PASS", NameAr = "مطابق", NameEn = "Compliant", Kind = OutcomeKind.Pass },
                new() { Code = "FAIL", NameAr = "غير مطابق", NameEn = "Not compliant", Kind = OutcomeKind.Fail },
            ],
            Fields = [new() { Code = "score", LabelAr = "الدرجة", LabelEn = "Score", Type = VisitFieldType.Number }],
        }));
        return type;
    }

    private static RequestService Requests(IServiceProvider services) => services.GetRequiredService<RequestService>();
}
