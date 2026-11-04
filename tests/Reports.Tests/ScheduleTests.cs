using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.Communication.Persistence;
using DynamicCube.Modules.Reports.Changes;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Modules.Reports.Persistence;
using DynamicCube.Modules.Reports.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Files;
using DynamicCube.Platform.Reports;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Reports.Tests;

/// <summary>Scheduled deliveries (ADR-0045): slots, recipients with their own access, emails with the file, the log.</summary>
public sealed class ScheduleTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly TimeZoneInfo Riyadh = TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh");
    private TestHost _host = null!;
    private string _class = null!;
    private string _report = null!;
    private string _schedule = null!;

    public async ValueTask InitializeAsync()
    {
        _host = new TestHost(fixture.RequireConnectionString());
        _class = TestHost.Unique("REGION");
        _report = TestHost.Unique("REGIONS");
        _schedule = TestHost.Unique("DAILY_REGIONS");
        await _host.PublishAsync(
            AppsControlAreas.DataDictionary,
            new Change(AppsControlChangeTypes.Class, _class, ChangeOperation.Create, new { nameAr = "مناطق", nameEn = "Regions", attributes = Array.Empty<object>() }),
            new Change(AppsControlChangeTypes.Domain, $"{_class}/R1", ChangeOperation.Create, new { nameAr = "الرياض", nameEn = "Riyadh" }),
            new Change(AppsControlChangeTypes.Domain, $"{_class}/R2", ChangeOperation.Create, new { nameAr = "مكة", nameEn = "Makkah" }));
        await _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Report, _report, ChangeOperation.Create, new ReportPayload
        {
            NameAr = "المناطق",
            NameEn = "Regions",
            Query = JsonNode.Parse($$"""
                { "from": { "kind": "CLASS", "code": "{{_class}}", "alias": "r" },
                  "where": { "match": "ALL", "conditions": [ { "column": "r.code", "op": "EQ", "param": "code" } ] } }
                """),
            Columns =
            [
                new() { Name = "code", LabelAr = "الرمز", LabelEn = "Code", Column = "r.code" },
                new() { Name = "name", LabelAr = "الاسم", LabelEn = "Name", Column = "r.name_ar" },
            ],
            Filters = [new() { Name = "code", LabelAr = "الرمز", LabelEn = "Code", Type = ReportFilterTypes.Text }],
        }));

        // The administrator has an email address; the maker may not open the report.
        await _host.AsAsync("admin", async sp =>
        {
            var db = sp.GetRequiredService<AppsControlDbContext>();
            _ = await sp.GetRequiredService<AppsControl.Access.AccessResolver>().GetAsync(CancellationToken.None);
            await db.Users.Where(u => u.Code == "admin").ExecuteUpdateAsync(u => u.SetProperty(x => x.Email, "admin@example.com"));
        });
        await _host.AsAsync("maker", sp => sp.GetRequiredService<AppsControl.Access.AccessResolver>().GetAsync(CancellationToken.None));
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<Guid> PublishScheduleAsync(Action<ReportSchedulePayload>? change = null)
    {
        var payload = new ReportSchedulePayload
        {
            NameAr = "المناطق يومياً",
            NameEn = "Regions daily",
            ReportCode = _report,
            Frequency = ScheduleFrequencies.Daily,
            Time = "07:00",
            Format = ReportFormats.Xlsx,
            Language = "EN",
            Users = ["admin", "maker", "nobody-here"],
            FilterValues = new() { ["code"] = "R1" },
        };
        change?.Invoke(payload);
        return _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Schedule, _schedule, ChangeOperation.Create, payload));
    }

    [Fact]
    public void Slots_are_the_latest_local_time_of_the_day_weekday_or_day_of_month()
    {
        var now = new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero); // Wednesday 08:00 in Riyadh
        ReportScheduler.LatestSlot(new ReportSchedule { Frequency = ScheduleFrequencies.Daily, Time = "07:30" }, now, Riyadh)
            .ShouldBe(new DateTimeOffset(2026, 11, 4, 4, 30, 0, TimeSpan.Zero));
        ReportScheduler.LatestSlot(new ReportSchedule { Frequency = ScheduleFrequencies.Daily, Time = "09:00" }, now, Riyadh)
            .ShouldBe(new DateTimeOffset(2026, 11, 3, 6, 0, 0, TimeSpan.Zero), "today's 09:00 has not come yet");
        ReportScheduler.LatestSlot(new ReportSchedule { Frequency = ScheduleFrequencies.Weekly, DayOfWeek = 0, Time = "07:00" }, now, Riyadh)
            .ShouldBe(new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero), "last Sunday");
        ReportScheduler.LatestSlot(new ReportSchedule { Frequency = ScheduleFrequencies.Monthly, DayOfMonth = 15, Time = "07:00" }, now, Riyadh)
            .ShouldBe(new DateTimeOffset(2026, 10, 15, 4, 0, 0, TimeSpan.Zero), "the 15th of last month");
    }

    [Fact]
    public async Task Each_recipient_gets_the_report_with_their_own_access_once_per_slot()
    {
        await PublishScheduleAsync();
        var delivery = await _host.AsAsync("admin", async sp =>
        {
            var schedule = await sp.GetRequiredService<ReportsDbContext>().Schedules.SingleAsync(s => s.Code == _schedule);
            return await sp.GetRequiredService<ReportScheduler>().RunAsync(schedule, DateTimeOffset.UtcNow, CancellationToken.None);
        });

        delivery.ShouldNotBeNull();
        delivery.Error.ShouldBeNull();
        delivery.Recipients.Select(r => (r.UserName, r.Outcome)).ShouldBe([("admin", "SENT"), ("maker", "SKIPPED"), ("nobody-here", "SKIPPED")]);
        delivery.Recipients[0].Rows.ShouldBe(1, "the schedule's filter: R1 only");
        (delivery.Sent, delivery.Skipped, delivery.Failed).ShouldBe((1, 2, 0));

        var message = await _host.AsAsync("admin", sp => sp.GetRequiredService<CommunicationDbContext>().Messages.AsNoTracking()
            .Where(m => m.SubjectId == delivery.Id || (m.EventCode == "REPORT_SCHEDULE" && m.Recipient == "admin" && m.Title!.Contains("Regions")))
            .OrderByDescending(m => m.CreatedAt).FirstAsync());
        message.Address.ShouldBe("admin@example.com");
        message.Title.ShouldBe("Scheduled report: Regions");
        var attachment = message.Attachments.ShouldHaveSingleItem();
        attachment.FileName.ShouldEndWith(".xlsx");
        (await _host.AsAsync("admin", sp => sp.GetRequiredService<IFileStorage>().ExistsAsync(attachment.Key, CancellationToken.None))).ShouldBeTrue();

        // The same slot does not run twice.
        var again = await _host.AsAsync("admin", async sp =>
        {
            var schedule = await sp.GetRequiredService<ReportsDbContext>().Schedules.AsNoTracking().SingleAsync(s => s.Code == _schedule);
            return await sp.GetRequiredService<ReportScheduler>().RunAsync(schedule, delivery.DueAt, CancellationToken.None);
        });
        again.ShouldBeNull();
    }

    [Fact]
    public async Task Due_schedules_run_once_and_a_new_schedule_waits_for_its_next_slot()
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Riyadh).AddMinutes(-5);
        await PublishScheduleAsync(p => p.Time = local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        int Run(IServiceProvider sp) => sp.GetRequiredService<ReportScheduler>().RunDueAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Published after today's slot: nothing until the next one.
        await _host.AsAsync("admin", sp => Task.FromResult(Run(sp)));
        (await _host.AsAsync("admin", sp => sp.GetRequiredService<ReportsDbContext>().Deliveries.CountAsync(d => d.ScheduleCode == _schedule))).ShouldBe(0);

        // As if it had been published yesterday: today's slot is due, once.
        await _host.AsAsync("admin", sp => sp.GetRequiredService<ReportsDbContext>().Schedules.Where(s => s.Code == _schedule)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, DateTimeOffset.UtcNow.AddDays(-1))));
        await _host.AsAsync("admin", sp => Task.FromResult(Run(sp)));
        await _host.AsAsync("admin", sp => Task.FromResult(Run(sp)));
        (await _host.AsAsync("admin", sp => sp.GetRequiredService<ReportsDbContext>().Deliveries.CountAsync(d => d.ScheduleCode == _schedule))).ShouldBe(1);
    }

    [Fact]
    public async Task Checks_the_schedule()
    {
        var errors = (await Should.ThrowAsync<ChangeValidationException>(() => PublishScheduleAsync(p =>
        {
            p.ReportCode = "MISSING";
            p.Frequency = ScheduleFrequencies.Weekly;
            p.DayOfWeek = 9;
            p.Time = "25:00";
            p.Format = "PDF";
            p.Users = [];
        }))).Errors.Keys.ToList();
        foreach (var field in new[] { "reportCode", "dayOfWeek", "time", "format", "users" })
        {
            errors.ShouldContain(k => k.EndsWith(field, StringComparison.Ordinal));
        }

        var filter = (await Should.ThrowAsync<ChangeValidationException>(() => PublishScheduleAsync(p => p.FilterValues = new() { ["other"] = "x" }))).Errors.Keys;
        filter.ShouldContain(k => k.EndsWith("filterValues.other", StringComparison.Ordinal));
    }
}
