using System.Globalization;
using System.Net;
using DynamicCube.Modules.Reports.Changes;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Modules.Reports.Persistence;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Reports;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.Reports.Runtime;

/// <summary>
/// Scheduled deliveries (ADR-0045): every minute, each active schedule whose latest slot (daily, a weekday or a day of the
/// month, at its local time) has not been covered runs once. Each recipient — the schedule's users and the holders of its
/// roles — gets the report run with their own access, as an Excel or CSV file by email; someone who may not open the
/// report, is inactive or has no email address is skipped. Every run is logged with each recipient's outcome.
/// </summary>
internal sealed partial class ReportScheduler(
    ReportsDbContext context,
    ReportService reports,
    IReportQueries queries,
    IStaffDirectory staff,
    IDirectMessages messages,
    TimeProvider clock,
    IOptions<ReportsOptions> options,
    ILogger<ReportScheduler> logger)
{
    public const int MaxRecipients = 300;

    /// <summary>Runs the schedules that are due; returns how many ran.</summary>
    public async Task<int> RunDueAsync(CancellationToken cancellationToken)
    {
        var zone = options.Value.Zone;
        var now = clock.GetUtcNow();
        var schedules = await context.Schedules.Where(s => s.IsActive).ToListAsync(cancellationToken);
        var ran = 0;
        foreach (var schedule in schedules)
        {
            var slot = LatestSlot(schedule, now, zone);
            if (slot is null || slot <= (schedule.LastRunAt ?? schedule.CreatedAt))
            {
                continue;
            }

            await RunAsync(schedule, slot.Value, cancellationToken);
            ran++;
        }

        return ran;
    }

    /// <summary>Runs a schedule for one slot (also "send now" from the screen): once per slot, logged.</summary>
    public async Task<ReportDelivery?> RunAsync(ReportSchedule schedule, DateTimeOffset slot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var delivery = new ReportDelivery { ScheduleCode = schedule.Code, ReportCode = schedule.ReportCode, DueAt = slot, StartedAt = clock.GetUtcNow() };
        context.Deliveries.Add(delivery);
        schedule.LastRunAt = slot;
        try
        {
            // The slot is claimed first: another worker running at the same moment fails here and leaves it.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return null;
        }

        try
        {
            var report = await context.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.Code == schedule.ReportCode && r.IsActive, cancellationToken);
            if (report is null)
            {
                delivery.Error = $"Report '{schedule.ReportCode}' does not exist or is not active.";
            }
            else
            {
                foreach (var recipient in await RecipientsAsync(schedule, cancellationToken))
                {
                    delivery.Recipients.Add(await SendAsync(schedule, report, recipient, cancellationToken));
                }
            }
        }
#pragma warning disable CA1031 // A failing run is logged on the delivery; the next schedules still run.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            delivery.Error = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            LogFailed(logger, ex, schedule.Code);
        }

        delivery.Sent = delivery.Recipients.Count(r => r.Outcome == "SENT");
        delivery.Skipped = delivery.Recipients.Count(r => r.Outcome == "SKIPPED");
        delivery.Failed = delivery.Recipients.Count(r => r.Outcome == "FAILED");
        delivery.FinishedAt = clock.GetUtcNow();
        delivery.Recipients = [.. delivery.Recipients];
        await context.SaveChangesAsync(CancellationToken.None);
        return delivery;
    }

    private async Task<ReportDeliveryRecipient> SendAsync(ReportSchedule schedule, ReportDefinition report, StaffMember member, CancellationToken cancellationToken)
    {
        var outcome = new ReportDeliveryRecipient { UserName = member.UserName };
        var viewer = new ReportViewer(member.UserName);
        var resource = ReportResources.Of(report.Code);
        if (!await queries.CanAsync(viewer, resource, Platform.Security.CatalogActions.View, cancellationToken))
        {
            return Skip(outcome, "May not open the report (or is not active).");
        }

        if (string.IsNullOrWhiteSpace(member.Email))
        {
            return Skip(outcome, "Has no email address.");
        }

        try
        {
            var arabic = schedule.Language != "EN";
            var (result, inputs) = await reports.ResultAsync(report, schedule.FilterValues, viewer, resource, ReportExports.MaxRows, 0, withSummary: true, cancellationToken);
            var at = options.Value.Now(clock);
            var file = ReportExports.Write(report, result, inputs, schedule.Format, arabic, at);
            var name = arabic ? report.NameAr : report.NameEn;
            var person = arabic ? member.NameAr : member.NameEn;
            var rows = result.Rows.Count.ToString(CultureInfo.InvariantCulture);
            var body = arabic
                ? $"<p dir=\"rtl\">مرحباً {WebUtility.HtmlEncode(person)}،</p><p dir=\"rtl\">مرفق تقرير <b>{WebUtility.HtmlEncode(name)}</b> المجدول ({rows} صفاً)، أُعد في {at:yyyy-MM-dd HH:mm} بصلاحياتك.</p>"
                : $"<p>Dear {WebUtility.HtmlEncode(person)},</p><p>Attached is the scheduled report <b>{WebUtility.HtmlEncode(name)}</b> ({rows} rows), prepared at {at:yyyy-MM-dd HH:mm} with your access.</p>";
            await messages.QueueEmailAsync(
                new DirectEmail(member.UserName, person, member.Email, (arabic ? "تقرير مجدول: " : "Scheduled report: ") + name, body, [new MessageAttachment(file.FileName, file.ContentType, file.Content)], "REPORT_SCHEDULE", "REPORT_SCHEDULE", schedule.Id),
                cancellationToken);
            outcome.Outcome = "SENT";
            outcome.Rows = result.Rows.Count;
        }
        catch (ReportAccessException ex)
        {
            return Skip(outcome, ex.Message);
        }
#pragma warning disable CA1031 // One recipient's failure is recorded; the others still get the report.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            outcome.Outcome = "FAILED";
            outcome.Message = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
        }

        return outcome;
    }

    /// <summary>The schedule's users and the holders of its roles, once each.</summary>
    private async Task<List<StaffMember>> RecipientsAsync(ReportSchedule schedule, CancellationToken cancellationToken)
    {
        var found = new List<StaffMember>();
        foreach (var user in schedule.Users)
        {
            found.Add(await staff.FindAsync(user, cancellationToken) ?? new StaffMember(user, user, user, [], [], []));
        }

        foreach (var role in schedule.Roles)
        {
            found.AddRange(await staff.SearchAsync(new StaffQuery(Role: role), cancellationToken));
        }

        return [.. found.DistinctBy(m => m.UserName, StringComparer.Ordinal).Take(MaxRecipients)];
    }

    private static ReportDeliveryRecipient Skip(ReportDeliveryRecipient outcome, string message)
    {
        outcome.Outcome = "SKIPPED";
        outcome.Message = message;
        return outcome;
    }

    /// <summary>The schedule's latest slot at or before <paramref name="now"/> (its local time in <paramref name="zone"/>), in UTC.</summary>
    public static DateTimeOffset? LatestSlot(ReportSchedule schedule, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (!TimeOnly.TryParseExact(schedule.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return null;
        }

        var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        for (var back = 0; back <= 62; back++)
        {
            var day = local.AddDays(-back);
            var matches = schedule.Frequency switch
            {
                ScheduleFrequencies.Weekly => schedule.DayOfWeek == (int)day.DayOfWeek,
                ScheduleFrequencies.Monthly => schedule.DayOfMonth == day.Day,
                _ => true,
            };
            if (!matches)
            {
                continue;
            }

            var at = day.ToDateTime(time);
            var slot = new DateTimeOffset(at, zone.GetUtcOffset(at)).ToUniversalTime();
            if (slot <= now)
            {
                return slot;
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report schedule {Schedule} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, string schedule);
}
