using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.Calendars;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

public sealed class StaffDirectoryTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    [Fact]
    public async Task Search_finds_active_employees_by_role_unit_and_position()
    {
        var type = Harness.Unique("TYPE");
        var unit = Harness.Unique("UNIT");
        var role = Harness.Unique("REVIEWER");
        var lead = Harness.Unique("lead").ToLowerInvariant();
        var member = Harness.Unique("member").ToLowerInvariant();
        var expired = Harness.Unique("expired").ToLowerInvariant();
        await _harness.PublishAsync(
            AppsControlAreas.Organization,
            new Change(AppsControlChangeTypes.OrgUnitType, type, ChangeOperation.Create, new { nameAr = "نوع", nameEn = "Type" }),
            new Change(AppsControlChangeTypes.OrgUnit, unit, ChangeOperation.Create, new { nameAr = "وحدة", nameEn = "Unit", typeCode = type }));
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new { nameAr = "مراجع", nameEn = "Reviewer" }),
            new Change(AppsControlChangeTypes.User, lead, ChangeOperation.Create, new
            {
                nameAr = "قائد",
                nameEn = "Lead",
                assignments = new[] { new { orgUnitCode = unit, positionCode = "MANAGER" } },
                roles = new[] { new { roleCode = role } },
            }),
            new Change(AppsControlChangeTypes.User, member, ChangeOperation.Create, new
            {
                nameAr = "عضو",
                nameEn = "Member",
                assignments = new[] { new { orgUnitCode = unit } },
                roles = new[] { new { roleCode = role } },
            }),
            new Change(AppsControlChangeTypes.User, expired, ChangeOperation.Create, new
            {
                nameAr = "منتهي",
                nameEn = "Expired",
                roles = new[] { new { roleCode = role, effectiveTo = "2020-01-01" } },
            }));

        var (byRole, managers, inUnit, found) = await _harness.AsAsync("admin", async sp =>
        {
            var directory = sp.GetRequiredService<IStaffDirectory>();
            return (
                await directory.SearchAsync(new StaffQuery(Role: role), CancellationToken.None),
                await directory.SearchAsync(new StaffQuery(OrgUnit: unit, Position: "MANAGER"), CancellationToken.None),
                await directory.SearchAsync(new StaffQuery(Role: role, OrgUnit: unit), CancellationToken.None),
                await directory.FindAsync(lead, CancellationToken.None));
        });

        byRole.Select(s => s.UserName).ShouldBe([lead, member], ignoreOrder: true);
        managers.Select(s => s.UserName).ShouldBe([lead]);
        inUnit.Count.ShouldBe(2);
        found.ShouldNotBeNull();
        found.NameEn.ShouldBe("Lead");
        found.Roles.ShouldBe([role]);
        found.OrgUnits.ShouldBe([unit]);
        found.Positions.ShouldBe(["MANAGER"]);
    }

    [Fact]
    public async Task The_signed_in_employee_is_the_current_staff_member()
    {
        var current = await _harness.AsAsync("checker", sp => sp.GetRequiredService<IStaffDirectory>().CurrentAsync(CancellationToken.None));

        current.ShouldNotBeNull();
        current.UserName.ShouldBe("checker");
        current.Roles.ShouldContain("CONFIG_CHECKER");
    }

    [Fact]
    public async Task Holidays_and_weekends_are_skipped_by_the_working_calendar()
    {
        var holiday = Harness.Unique("HOLIDAY");
        await _harness.PublishAsync(
            AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Holiday, holiday, ChangeOperation.Create, new { nameAr = "عيد", nameEn = "Eid", fromDate = "2031-03-23", toDate = "2031-03-25" }));

        var calendar = await _harness.AsAsync("admin", sp => sp.GetRequiredService<IWorkingCalendarProvider>().GetAsync(CancellationToken.None));

        // Thursday 2031-03-20 + 2 working days: Fri/Sat weekend, Sun 23 – Tue 25 holiday → Wed 26, Thu 27.
        var start = new DateTimeOffset(2031, 3, 20, 10, 0, 0, TimeSpan.FromHours(3));
        calendar.AddWorkingDays(start, 2).ShouldBe(new DateTimeOffset(2031, 3, 27, 10, 0, 0, TimeSpan.FromHours(3)));
        calendar.IsWorkingDay(new DateOnly(2031, 3, 24)).ShouldBeFalse();

        var invalid = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Holiday, Harness.Unique("BAD"), ChangeOperation.Create, new { nameAr = "س", nameEn = "S", fromDate = "2031-03-25", toDate = "2031-03-23" })));
        invalid.Errors.Keys.ShouldContain("toDate");
    }
}
