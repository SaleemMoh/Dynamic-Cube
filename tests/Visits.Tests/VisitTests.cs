using System.Text.Json.Nodes;
using DynamicCube.Modules.Visits.Changes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Visits;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Visits.Tests;

public sealed class VisitTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_field_visit_is_scheduled_reported_once_from_the_field_app_and_tells_the_listeners()
    {
        await using var host = new VisitHost(fixture);
        var (role, inspector) = await host.InspectorAsync();
        var type = await host.FieldTypeAsync(role);
        var subject = Guid.CreateVersion7();

        var visit = await host.RequestAsync(type, subject);
        visit.Status.ShouldBe("PENDING");
        visit.Number.ShouldStartWith($"VIS-{host.Clock.Now.UtcDateTime.Year}-");
        visit.DueAt!.Value.ShouldBe(host.Clock.Now.AddDays(4), TimeSpan.FromMilliseconds(1));

        Task<VisitDetail> Staff(Func<VisitRegistry, Task<VisitDetail>> action) => host.AsAsync("officer", sp => action(sp.GetRequiredService<VisitRegistry>()));
        Task<T> Field<T>(Func<FieldVisits, Task<T>> action) => host.AsAsync(inspector, sp => action(sp.GetRequiredService<FieldVisits>()));

        // Only holders of the type's inspector role can be assigned.
        var wrong = await Should.ThrowAsync<VisitValidationException>(() => Staff(r => r.ScheduleAsync(visit.Id, new ScheduleVisitRequest("admin", host.Clock.Now.AddDays(1), null), CancellationToken.None)));
        wrong.Errors.Keys.ShouldContain("inspector");
        (await host.AsAsync("officer", sp => sp.GetRequiredService<VisitRegistry>().InspectorsAsync(type, CancellationToken.None))).Select(i => i.UserName).ShouldBe([inspector]);

        var at = host.Clock.Now.AddDays(1);
        var scheduled = await Staff(r => r.ScheduleAsync(visit.Id, new ScheduleVisitRequest(inspector, at, null), CancellationToken.None));
        scheduled.Visit.Status.ShouldBe("SCHEDULED");
        scheduled.Visit.Inspector.ShouldBe(inspector);

        // The field app gets the visit with what to record, so it can work offline.
        var mine = (await Field(f => f.MineAsync(CancellationToken.None))).ShouldHaveSingleItem();
        mine.Visit.Id.ShouldBe(visit.Id);
        mine.Type.Fields.Count.ShouldBe(4);
        mine.Type.RequireSignature.ShouldBeTrue();
        (await Field(f => f.StartAsync(visit.Id, CancellationToken.None))).Visit.Status.ShouldBe("IN_PROGRESS");

        // Everything the type requires is checked at once.
        var incomplete = await Should.ThrowAsync<VisitValidationException>(() => Field(f => f.ReportAsync(visit.Id, new FieldReport("sub-1", "MAYBE", new JsonObject { ["score"] = "high", ["signage"] = "BROKEN" }, null, [], null, null, null, null, null, null), CancellationToken.None)));
        incomplete.Errors.Keys.ShouldBe(["outcome", "data.notes", "data.score", "data.signage", "photos", "signature", "signedBy", "location"], ignoreOrder: true);

        var report = new FieldReport(
            "sub-1",
            "PASS",
            new JsonObject { ["notes"] = "الموقع مطابق", ["score"] = "92", ["signage"] = "OK", ["safe"] = true, ["unknown"] = "dropped" },
            "Visited with the owner.",
            [new FieldPhoto("front.png", "data:image/png;base64," + VisitHost.Png, "الواجهة", host.Clock.Now)],
            VisitHost.Png,
            "صاحب المنشأة",
            24.71,
            46.68,
            8,
            host.Clock.Now);
        var done = await Field(f => f.ReportAsync(visit.Id, report, CancellationToken.None));
        done.Visit.Status.ShouldBe("COMPLETED");
        done.Visit.Outcome.ShouldBe("PASS");
        done.Data.Select(p => p.Key).ShouldBe(["notes", "score", "signage", "safe"], ignoreOrder: true);
        done.Data["notes"]!.GetValue<string>().ShouldBe("الموقع مطابق");
        done.Data["score"]!.GetValue<decimal>().ShouldBe(92m);
        done.Data["safe"]!.GetValue<bool>().ShouldBeTrue();
        done.Photos.ShouldHaveSingleItem().ContentType.ShouldBe("image/png");
        done.HasSignature.ShouldBeTrue();
        done.CapturedLatitude.ShouldBe(24.71);

        var closed = host.Listener.Closed.ShouldHaveSingleItem();
        closed.Completed.ShouldBeTrue();
        closed.Outcome.ShouldBe("PASS");
        closed.OutcomeKind.ShouldBe("PASS");
        closed.Data["score"]!.GetValue<decimal>().ShouldBe(92m);

        // The outbox sent the same report again: nothing changes.
        (await Field(f => f.ReportAsync(visit.Id, report, CancellationToken.None))).Visit.Status.ShouldBe("COMPLETED");
        host.Listener.Closed.Count.ShouldBe(1);
        await Should.ThrowAsync<VisitConflictException>(() => Field(f => f.ReportAsync(visit.Id, report with { SubmissionId = "sub-2" }, CancellationToken.None)));

        // The photo can be read back, by staff and by the inspector; others do not see the visit.
        var photo = done.Photos.Single();
        await using (var stream = (await host.AsAsync("officer", sp => sp.GetRequiredService<VisitRegistry>().PhotoAsync(visit.Id, photo.Id, CancellationToken.None))).Content)
        {
            stream.ReadByte().ShouldBe(0x89);
        }

        await Should.ThrowAsync<VisitNotFoundException>(() => host.AsAsync("someone", sp => sp.GetRequiredService<FieldVisits>().GetAsync(visit.Id, CancellationToken.None)));
        var raised = await host.Host.RaisedAsync(visit.Id);
        raised.Select(e => e.Code).ShouldBe(["VISIT_REQUESTED", "VISIT_SCHEDULED", "VISIT_ASSIGNED", "VISIT_COMPLETED"], ignoreOrder: true);
        raised.Single(e => e.Code == "VISIT_ASSIGNED").Facts["visit"]!["inspector"]!.GetValue<string>().ShouldBe(inspector);
    }

    [Fact]
    public async Task Staff_record_manual_results_and_their_cancellations_tell_the_listeners_but_the_callers_do_not()
    {
        await using var host = new VisitHost(fixture);
        var type = await host.FieldTypeAsync(null, VisitMode.Manual, strict: false);
        var registry = (IServiceProvider sp) => sp.GetRequiredService<VisitRegistry>();

        var manual = await host.RequestAsync(type, Guid.CreateVersion7());
        var invalid = await Should.ThrowAsync<VisitValidationException>(() => host.AsAsync("officer", sp => registry(sp).RecordAsync(manual.Id, new RecordOutcomeRequest("FAIL", [], null, null), CancellationToken.None)));
        invalid.Errors.Keys.ShouldBe(["data.notes"]);
        var recorded = await host.AsAsync("officer", sp => registry(sp).RecordAsync(manual.Id, new RecordOutcomeRequest("FAIL", new JsonObject { ["notes"] = "مغلق" }, "Closed site", "phoned"), CancellationToken.None));
        recorded.Visit.Status.ShouldBe("COMPLETED");
        recorded.Events[^1].Type.ShouldBe(VisitEventTypes.Recorded);
        host.Listener.Closed.ShouldHaveSingleItem().OutcomeKind.ShouldBe("FAIL");

        var cancelled = await host.RequestAsync(type, Guid.CreateVersion7());
        await Should.ThrowAsync<VisitValidationException>(() => host.AsAsync("officer", sp => registry(sp).CancelAsync(cancelled.Id, new CancelVisitRequest(" "), CancellationToken.None)));
        (await host.AsAsync("officer", sp => registry(sp).CancelAsync(cancelled.Id, new CancelVisitRequest("Duplicate"), CancellationToken.None))).Visit.Status.ShouldBe("CANCELLED");
        host.Listener.Closed.Count.ShouldBe(2);
        host.Listener.Closed.Last().Completed.ShouldBeFalse();

        // The workflow cancels visits it no longer needs: it is not told about its own cancellation.
        var unneeded = await host.RequestAsync(type, Guid.CreateVersion7());
        await host.AsAsync("officer", async sp =>
        {
            await sp.GetRequiredService<IVisits>().CancelAsync(unneeded.Id, "no longer needed", CancellationToken.None);
            return true;
        });
        host.Listener.Closed.Count.ShouldBe(2);
        (await host.AsAsync("officer", sp => registry(sp).GetAsync(unneeded.Id, CancellationToken.None))).Visit.Status.ShouldBe("CANCELLED");
        await Should.ThrowAsync<VisitConflictException>(() => host.AsAsync("officer", sp => registry(sp).RecordAsync(unneeded.Id, new RecordOutcomeRequest("PASS", new JsonObject { ["notes"] = "x" }, null, null), CancellationToken.None)));
    }

    [Fact]
    public async Task An_external_visit_is_booked_and_polled_until_the_external_system_reports_its_result()
    {
        await using var host = new VisitHost(fixture);
        var type = await host.ExternalTypeAsync();
        host.NextReference = "INSP-" + Guid.NewGuid().ToString("N")[..8];

        var visit = await host.RequestAsync(type, Guid.CreateVersion7(), new JsonObject { ["data"] = new JsonObject { ["activity"] = "مطعم" } });
        visit.Status.ShouldBe("SENT");
        var booked = JsonNode.Parse(host.Host.Provider.Requests.Last(r => r.Url.AbsolutePath == "/v1/inspections").Body!)!;
        booked["visitNumber"]!.GetValue<string>().ShouldBe(visit.Number);
        booked["activity"]!.GetValue<string>().ShouldBe("مطعم");
        booked["siteAddress"]!.GetValue<string>().ShouldBe("الرياض، حي العليا");

        var detail = await host.AsAsync("officer", sp => sp.GetRequiredService<VisitRegistry>().GetAsync(visit.Id, CancellationToken.None));
        detail.ExternalReference.ShouldBe(host.NextReference);
        detail.NextPollAt!.Value.ShouldBe(host.Clock.Now.AddMinutes(10), TimeSpan.FromMilliseconds(1));

        Task<int> PollAsync() => host.AsAsync("system", sp => sp.GetRequiredService<ExternalVisits>().PollAsync(CancellationToken.None));

        // Not due yet; then due, but still in progress at the external system.
        (await PollAsync()).ShouldBe(0);
        host.Clock.Now = host.Clock.Now.AddMinutes(11);
        host.Result = """{ "status": "IN_PROGRESS" }""";
        (await PollAsync()).ShouldBeGreaterThanOrEqualTo(1);
        detail = await host.AsAsync("officer", sp => sp.GetRequiredService<VisitRegistry>().GetAsync(visit.Id, CancellationToken.None));
        detail.Visit.Status.ShouldBe("SENT");
        detail.ExternalStatus.ShouldBe("IN_PROGRESS");

        // Finished: the external outcome is mapped and the findings kept.
        host.Clock.Now = host.Clock.Now.AddMinutes(11);
        host.Result = """{ "status": "COMPLETED", "result": "NON_COMPLIANT", "findings": { "violations": 2 }, "summary": "Fire exit blocked" }""";
        (await PollAsync()).ShouldBeGreaterThanOrEqualTo(1);
        detail = await host.AsAsync("officer", sp => sp.GetRequiredService<VisitRegistry>().GetAsync(visit.Id, CancellationToken.None));
        detail.Visit.Status.ShouldBe("COMPLETED");
        detail.Visit.Outcome.ShouldBe("FAIL");
        detail.Data["violations"]!.GetValue<int>().ShouldBe(2);
        detail.Report.ShouldBe("Fire exit blocked");
        host.Listener.Closed.Where(c => c.VisitId == visit.Id).ShouldHaveSingleItem().Outcome.ShouldBe("FAIL");
        await Should.ThrowAsync<VisitConflictException>(() => host.AsAsync("officer", sp => sp.GetRequiredService<ExternalVisits>().PollOneAsync(visit.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task A_visit_type_needs_outcomes_valid_fields_and_for_external_visits_how_results_come()
    {
        await using var host = new VisitHost(fixture);
        var errors = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(VisitChangeTypes.Area, new Change(VisitChangeTypes.Type, TestHost.Unique("BAD"), ChangeOperation.Create, new VisitTypePayload
        {
            NameAr = "س",
            NameEn = "S",
            Mode = VisitMode.External,
            DueDays = 0,
            PhotosRequired = 20,
            Fields =
            [
                new() { Code = "1st", LabelAr = "أ", LabelEn = "A" },
                new() { Code = "kind", LabelAr = "ب", LabelEn = "B", Type = VisitFieldType.Select },
            ],
            External = new ExternalVisitSettings { CreateOperation = "OP", OutcomeMap = new() { ["OK"] = "NOPE" } },
        })));
        errors.Errors.Keys.ShouldBe(["dueDays", "photosRequired", "outcomes", "fields[0].code", "fields[1].options", "external", "external.outcomeMap.OK"], ignoreOrder: true);
    }
}
