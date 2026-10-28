using DynamicCube.Platform.Calendars;
using Shouldly;

namespace DynamicCube.Platform.Tests;

public sealed class WorkingCalendarTests
{
    private static readonly TimeSpan Riyadh = TimeSpan.FromHours(3);

    [Fact]
    public void Adding_working_days_skips_the_weekend_and_keeps_the_time_of_day()
    {
        var calendar = WorkingCalendar.Default;
        var thursday = new DateTimeOffset(2026, 10, 29, 14, 30, 0, Riyadh);

        calendar.AddWorkingDays(thursday, 1).ShouldBe(new DateTimeOffset(2026, 11, 1, 14, 30, 0, Riyadh));
        calendar.AddWorkingDays(thursday, 5).ShouldBe(new DateTimeOffset(2026, 11, 5, 14, 30, 0, Riyadh));
        calendar.AddWorkingDays(thursday, 0).ShouldBe(thursday);
    }

    [Fact]
    public void Working_days_between_two_moments_count_only_working_days()
    {
        var calendar = new WorkingCalendar([5, 6], [new HolidayPeriod(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2))], TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"));
        var thursday = new DateTimeOffset(2026, 10, 29, 9, 0, 0, Riyadh);

        calendar.WorkingDaysBetween(thursday, thursday.AddDays(7)).ShouldBe(4);
        calendar.WorkingDaysBetween(thursday, thursday.AddDays(-3)).ShouldBe(0);
    }

    [Fact]
    public void Days_are_counted_in_the_installation_time_zone()
    {
        // 22:30 UTC on Thursday is already Friday in Riyadh: the next working day is Sunday, then Monday.
        var utc = new DateTimeOffset(2026, 10, 29, 22, 30, 0, TimeSpan.Zero);

        WorkingCalendar.Default.AddWorkingDays(utc, 2).ShouldBe(new DateTimeOffset(2026, 11, 2, 1, 30, 0, Riyadh));
    }
}
