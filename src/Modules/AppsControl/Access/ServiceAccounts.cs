using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Service accounts (ADR-0042): users of kind SERVICE that API clients act as, created with no roles.</summary>
internal sealed class ServiceAccounts(AppsControlDbContext context, AccessCache cache) : IServiceAccounts
{
    public async Task<ServiceAccountInfo?> FindAsync(string code, CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking()
            .Where(u => u.Code == code && u.Kind == UserKind.Service)
            .Select(u => new ServiceAccountInfo(u.Code, u.NameAr, u.NameEn, u.IsActive && u.Status == UserStatus.Active))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsPersonAsync(string code, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().AnyAsync(u => u.Code == code && u.Kind == UserKind.Person, cancellationToken);

    public async Task EnsureAsync(string code, string nameAr, string nameEn, CancellationToken cancellationToken)
    {
        var existing = await context.Users.AsNoTracking().Where(u => u.Code == code).Select(u => (UserKind?)u.Kind).SingleOrDefaultAsync(cancellationToken);
        if (existing == UserKind.Person)
        {
            throw new InvalidOperationException($"{code} is a person's account, not a service account.");
        }

        if (existing is not null)
        {
            return;
        }

        context.Users.Add(new UserAccount
        {
            Code = code,
            Kind = UserKind.Service,
            AuthSource = "SERVICE",
            NameAr = nameAr,
            NameEn = nameEn,
            Owner = Ownership.Entity,
        });
        await context.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
    }

    public async Task GiveRoleAsync(
        string code,
        string roleCode,
        string roleNameAr,
        string roleNameEn,
        IReadOnlyList<string> resources,
        IReadOnlyList<ServiceAccountHolding> holdings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(holdings);
        var user = await context.Users.Include(u => u.Roles).SingleOrDefaultAsync(u => u.Code == code && u.Kind == UserKind.Service, cancellationToken)
            ?? throw new InvalidOperationException($"There is no service account {code}.");
        var role = await context.Roles.Include(r => r.Privileges).SingleOrDefaultAsync(r => r.Code == roleCode, cancellationToken);
        if (role is null)
        {
            role = new Role { Code = roleCode, NameAr = roleNameAr, NameEn = roleNameEn, Owner = Ownership.Entity };
            context.Roles.Add(role);
        }

        var ids = await context.Resources.AsNoTracking().Where(r => resources.Contains(r.Code)).Select(r => r.Id).ToListAsync(cancellationToken);
        foreach (var id in ids.Where(id => role.Privileges.All(p => p.ResourceId != id || p.CompanyCode != null)))
        {
            role.Privileges.Add(new Privilege { RoleId = role.Id, ResourceId = id, Actions = [PrivilegeActions.View] });
        }

        foreach (var holding in holdings.Where(h => user.Roles.All(r => r.RoleId != role.Id || r.CompanyCode != h.Company)))
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, CompanyCode = holding.Company, Branches = [.. holding.Branches] });
        }

        await context.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
    }
}
