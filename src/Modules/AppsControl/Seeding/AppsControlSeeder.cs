using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Lookups;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Seeding;

/// <summary>
/// Inserts and updates the SYSTEM rows of <see cref="SystemCatalog"/> and of every module's
/// <see cref="SystemCatalogContribution"/> and creates the singleton installation and entity
/// rows (ENTITY-owned) when missing. Idempotent; run by the migrator after migrations.
/// </summary>
public sealed class AppsControlSeeder(
    AppsControlDbContext context,
    IEnumerable<ChangeAreaDefinition> changeAreas,
    IEnumerable<SystemCatalogContribution> contributions)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        if (!await context.Installations.AnyAsync(cancellationToken))
        {
            context.Installations.Add(new Installation { Code = Installation.SingletonCode, NameAr = "ديناميك كيوب", NameEn = "Dynamic Cube" });
        }

        if (!await context.Entities.AnyAsync(cancellationToken))
        {
            context.Entities.Add(new GovernmentEntity { Code = GovernmentEntity.SingletonCode, NameAr = "الجهة", NameEn = "Entity" });
        }

        // Approval policy rows belong to the entity: added with the product default when missing, never overwritten.
        var areas = await context.ChangeAreas.Select(a => a.Code).ToListAsync(cancellationToken);
        foreach (var area in changeAreas.Where(a => !areas.Contains(a.Code)))
        {
            context.ChangeAreas.Add(new ChangeArea { Code = area.Code, NameAr = area.NameAr, NameEn = area.NameEn, RequiresApproval = area.RequiresApprovalByDefault });
        }

        var apps = await UpsertAsync(context.Apps, SystemCatalog.Apps.Select(a => (a.Code, a.NameAr, a.NameEn, (Action<App>)(e =>
        {
            e.AppType = a.Type;
            e.DisplayOrder = a.Order;
        }))), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var catalog = contributions.ToList();
        var definitions = catalog.SelectMany(c => c.Resources).ToList();
        var resources = await UpsertAsync(context.Resources, definitions.Select(r => (r.Code, r.NameAr, r.NameEn, (Action<Resource>)(e =>
        {
            e.AppId = apps[r.App].Id;
            e.ResourceType = Enum.Parse<ResourceType>(r.Type, ignoreCase: true);
            e.AvailableActions = [.. r.Actions];
        }))), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        foreach (var definition in definitions.Where(r => r.Parent is not null))
        {
            resources[definition.Code].ParentId = resources[definition.Parent!].Id;
        }

        var roles = await UpsertAsync(
            context.Roles.Include(r => r.Privileges),
            SystemCatalog.Roles.Select(r => (r.Key, r.Value.NameAr, r.Value.NameEn, (Action<Role>)(_ => { }))),
            cancellationToken);
        foreach (var code in SystemCatalog.Roles.Keys)
        {
            var role = roles[code];
            foreach (var resource in definitions)
            {
                var actions = SystemCatalog.ActionsOf(code, resource);
                var privilege = role.Privileges.SingleOrDefault(p => p.ResourceId == resources[resource.Code].Id);
                if (actions.Count == 0)
                {
                    if (privilege is not null)
                    {
                        role.Privileges.Remove(privilege);
                    }

                    continue;
                }

                if (privilege is null)
                {
                    role.Privileges.Add(new Privilege { RoleId = role.Id, ResourceId = resources[resource.Code].Id, Actions = [.. actions] });
                }
                else if (!privilege.Actions.SequenceEqual(actions))
                {
                    privilege.Actions = [.. actions];
                }
            }
        }

        var groupDefinitions = catalog.Where(c => c.MenuGroup is not null).Select(c => c.MenuGroup!).ToList();
        var groups = await UpsertAsync(context.MenuGroups, groupDefinitions.Select(g => (g.Code, g.NameAr, g.NameEn, (Action<MenuGroup>)(e =>
        {
            e.AppId = apps[g.App].Id;
            e.DisplayOrder = g.Order;
            e.Icon = g.Icon;
        }))), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var contribution in catalog.Where(c => c.Menus.Count > 0))
        {
            var group = contribution.MenuGroup ?? throw new InvalidOperationException($"Module '{contribution.Module}' contributes menus without a menu group.");
            await UpsertAsync(context.Menus, contribution.Menus.Select(m => (m.Code, m.NameAr, m.NameEn, (Action<Menu>)(e =>
            {
                e.AppId = apps[group.App].Id;
                e.MenuGroupId = groups[group.Code].Id;
                e.ItemType = MenuItemType.Screen;
                e.Target = m.Target;
                e.ResourceId = resources[m.Resource].Id;
                e.Icon = m.Icon;
                e.DisplayOrder = m.Order;
                e.IsVisible = true;
            }))), cancellationToken);
        }

        await UpsertAsync(context.Positions, SystemCatalog.Positions.Select(p => (p.Code, p.NameAr, p.NameEn, (Action<Position>)(_ => { }))), cancellationToken);

        var classes = await UpsertAsync(context.Classes, SystemCatalog.Classes.Select(c => (c.Code, c.NameAr, c.NameEn, (Action<LookupClass>)(_ => { }))), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        foreach (var definition in SystemCatalog.Classes)
        {
            var classId = classes[definition.Code].Id;
            await UpsertAsync(
                context.Domains.Where(d => d.ClassId == classId),
                definition.Values.Select((v, i) => (v.Code, v.NameAr, v.NameEn, (Action<DomainValue>)(d =>
                {
                    d.ClassId = classId;
                    d.DisplayOrder = (i + 1) * 10;
                }))),
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await ClassViewGenerator.RegenerateAsync(context, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Adds missing SYSTEM rows and refreshes existing ones; returns all rows of <paramref name="definitions"/> by code.</summary>
    private async Task<Dictionary<string, T>> UpsertAsync<T>(
        IQueryable<T> set,
        IEnumerable<(string Code, string NameAr, string NameEn, Action<T> Apply)> definitions,
        CancellationToken cancellationToken)
        where T : ConfigEntity, new()
    {
        var list = definitions.ToList();
        var codes = list.Select(d => d.Code).ToList();
        var existing = await set.Where(e => codes.Contains(e.Code)).ToDictionaryAsync(e => e.Code, cancellationToken);
        foreach (var (code, nameAr, nameEn, apply) in list)
        {
            if (!existing.TryGetValue(code, out var entity))
            {
                entity = new T { Code = code };
                context.Add(entity);
                existing[code] = entity;
            }

            entity.Owner = Ownership.System;
            entity.NameAr = nameAr;
            entity.NameEn = nameEn;
            entity.IsActive = true;
            apply(entity);
        }

        return existing;
    }
}
