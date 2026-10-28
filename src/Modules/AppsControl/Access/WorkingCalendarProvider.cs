using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Calendars;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>The working calendar from the installation (weekend days, time zone) and the holidays; cached until Apps Control publishes.</summary>
internal sealed class WorkingCalendarProvider(AppsControlDbContext context, IMemoryCache cache, AccessCache version) : IWorkingCalendarProvider
{
    public async Task<WorkingCalendar> GetAsync(CancellationToken cancellationToken)
    {
        var key = $"apps-control:calendar:{version.Version}";
        if (cache.TryGetValue(key, out WorkingCalendar? cached) && cached is not null)
        {
            return cached;
        }

        var installation = await context.Installations.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var holidays = await context.Holidays.AsNoTracking().Where(h => h.IsActive).Select(h => new HolidayPeriod(h.FromDate, h.ToDate)).ToListAsync(cancellationToken);
        var timeZone = installation is not null && TimeZoneInfo.TryFindSystemTimeZoneById(installation.TimeZone, out var zone) ? zone : WorkingCalendar.Default.TimeZone;
        var calendar = new WorkingCalendar(installation?.WeekendDays ?? [5, 6], holidays, timeZone);
        cache.Set(key, calendar, TimeSpan.FromMinutes(10));
        return calendar;
    }
}
