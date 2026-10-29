using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Active employees with their roles, units and positions effective today.</summary>
internal sealed class StaffDirectory(AppsControlDbContext context, AccessResolver access, TimeProvider clock) : IStaffDirectory
{
    private const int MaxResults = 500;

    public async Task<StaffMember?> CurrentAsync(CancellationToken cancellationToken)
    {
        var snapshot = await access.GetAsync(cancellationToken);
        var user = snapshot.User;
        return user.UserId is null || !snapshot.IsActive
            ? null
            : new StaffMember(user.UserName, snapshot.NameAr ?? user.UserName, snapshot.NameEn ?? user.UserName, user.Roles, user.OrgUnits, user.Positions);
    }

    public async Task<StaffMember?> FindAsync(string userName, CancellationToken cancellationToken) =>
        (await LoadAsync(Active().Where(u => u.Code == userName), cancellationToken)).SingleOrDefault();

    public async Task<IReadOnlyList<StaffMember>> SearchAsync(StaffQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var today = Today();
        var users = Active();
        if (query.Role is { } role)
        {
            users = users.Where(u => context.UserRoles.Any(r => r.UserId == u.Id && r.Role!.Code == role && r.Role.IsActive
                && (r.EffectiveFrom == null || r.EffectiveFrom <= today) && (r.EffectiveTo == null || r.EffectiveTo >= today)));
        }

        if (query.OrgUnit is not null || query.Position is not null || query.OrgUnits is not null)
        {
            var unit = query.OrgUnit;
            var units = query.OrgUnits?.ToList();
            var position = query.Position;
            users = users.Where(u => context.UserAssignments.Any(a => a.UserId == u.Id
                && (unit == null || a.OrgUnit!.Code == unit) && (units == null || units.Contains(a.OrgUnit!.Code))
                && (position == null || a.Position!.Code == position)
                && (a.EffectiveFrom == null || a.EffectiveFrom <= today) && (a.EffectiveTo == null || a.EffectiveTo >= today)));
        }

        return await LoadAsync(users.OrderBy(u => u.Code).Take(MaxResults), cancellationToken);
    }

    public async Task<IReadOnlyList<OrgUnitNode>> OrgUnitsAsync(CancellationToken cancellationToken)
    {
        var today = Today();
        return await context.OrgUnits.AsNoTracking()
            .Where(u => u.IsActive && (u.EffectiveFrom == null || u.EffectiveFrom <= today) && (u.EffectiveTo == null || u.EffectiveTo >= today))
            .OrderBy(u => u.Code)
            .Select(u => new OrgUnitNode(u.Code, u.Parent != null ? u.Parent.Code : null, u.NameAr, u.NameEn))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<UserAccount> Active() => context.Users.AsNoTracking().Where(u => u.IsActive && u.Status == UserStatus.Active);

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private async Task<IReadOnlyList<StaffMember>> LoadAsync(IQueryable<UserAccount> users, CancellationToken cancellationToken)
    {
        var today = Today();
        var rows = await users.Select(u => new { u.Id, u.Code, u.NameAr, u.NameEn, u.Email, u.Mobile }).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var roles = await context.UserRoles.AsNoTracking()
            .Where(r => ids.Contains(r.UserId) && r.Role!.IsActive && (r.EffectiveFrom == null || r.EffectiveFrom <= today) && (r.EffectiveTo == null || r.EffectiveTo >= today))
            .Select(r => new { r.UserId, r.Role!.Code })
            .ToListAsync(cancellationToken);
        var assignments = await context.UserAssignments.AsNoTracking()
            .Where(a => ids.Contains(a.UserId) && (a.EffectiveFrom == null || a.EffectiveFrom <= today) && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .OrderByDescending(a => a.IsPrimary)
            .Select(a => new { a.UserId, Unit = a.OrgUnit!.Code, Position = a.Position != null ? a.Position.Code : null })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new StaffMember(
            r.Code,
            r.NameAr,
            r.NameEn,
            [.. roles.Where(x => x.UserId == r.Id).Select(x => x.Code).Distinct()],
            [.. assignments.Where(x => x.UserId == r.Id).Select(x => x.Unit).Distinct()],
            [.. assignments.Where(x => x.UserId == r.Id && x.Position != null).Select(x => x.Position!).Distinct()],
            r.Email,
            r.Mobile,
            [.. assignments.Where(x => x.UserId == r.Id).Select(x => new StaffPlacement(x.Unit, x.Position)).Distinct()]))];
    }
}
