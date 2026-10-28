namespace DynamicCube.Platform.Calendars;

/// <summary>A non-working period (public holiday), inclusive.</summary>
public sealed record HolidayPeriod(DateOnly From, DateOnly To);

/// <summary>
/// The installation's working calendar (weekend days, holidays, time zone), used for SLA due dates. Days are counted in
/// the installation's time zone; a deadline keeps the time of day it started at.
/// </summary>
public sealed class WorkingCalendar
{
    private readonly HashSet<DayOfWeek> _weekend;
    private readonly IReadOnlyList<HolidayPeriod> _holidays;

    /// <param name="weekendDays">ISO day numbers (1 = Monday … 7 = Sunday).</param>
    public WorkingCalendar(IEnumerable<int> weekendDays, IEnumerable<HolidayPeriod> holidays, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        _weekend = [.. weekendDays.Select(d => (DayOfWeek)(d % 7))];
        _holidays = [.. holidays];
        TimeZone = timeZone;
    }

    public TimeZoneInfo TimeZone { get; }

    /// <summary>Saudi defaults: Friday and Saturday off, no holidays, Riyadh time.</summary>
    public static WorkingCalendar Default { get; } = new([5, 6], [], TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"));

    public bool IsWorkingDay(DateOnly date) => !_weekend.Contains(date.DayOfWeek) && !_holidays.Any(h => date >= h.From && date <= h.To);

    /// <summary>The moment (UTC) <paramref name="days"/> working days after <paramref name="start"/>, at the same local time of day.</summary>
    public DateTimeOffset AddWorkingDays(DateTimeOffset start, int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);
        var local = TimeZoneInfo.ConvertTime(start, TimeZone);
        var date = DateOnly.FromDateTime(local.DateTime);
        var remaining = days;
        while (remaining > 0)
        {
            date = date.AddDays(1);
            if (IsWorkingDay(date))
            {
                remaining--;
            }
        }

        var target = date.ToDateTime(TimeOnly.FromDateTime(local.DateTime));
        return new DateTimeOffset(target, TimeZone.GetUtcOffset(target)).ToUniversalTime();
    }

    /// <summary>Whole working days from <paramref name="from"/> to <paramref name="to"/> (0 when <paramref name="to"/> is earlier).</summary>
    public int WorkingDaysBetween(DateTimeOffset from, DateTimeOffset to)
    {
        var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(from, TimeZone).DateTime);
        var end = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(to, TimeZone).DateTime);
        var count = 0;
        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            if (IsWorkingDay(date))
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>Supplies the current working calendar (implemented by Apps Control from the installation and its holidays).</summary>
public interface IWorkingCalendarProvider
{
    Task<WorkingCalendar> GetAsync(CancellationToken cancellationToken);
}
