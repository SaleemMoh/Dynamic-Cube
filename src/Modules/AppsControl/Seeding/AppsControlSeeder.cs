using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Lookups;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Lookups;
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

        if (!await context.Entities.AnyAsync(e => e.Code == GovernmentEntity.SingletonCode, cancellationToken))
        {
            context.Entities.Add(new GovernmentEntity { Code = GovernmentEntity.SingletonCode, NameAr = "الجهة", NameEn = "Entity" });
            await context.SaveChangesAsync(cancellationToken);
        }

        await SeedCompaniesAsync(cancellationToken);

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

        foreach (var retired in await context.Menus.Where(m => SystemCatalog.RetiredMenus.Contains(m.Code) && m.IsVisible).ToListAsync(cancellationToken))
        {
            retired.IsVisible = false;
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
        await SeedModuleClassesAsync(catalog.SelectMany(c => c.Classes).ToList(), cancellationToken);
        await ClassViewGenerator.RegenerateAsync(context, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The modules' classes: SYSTEM-owned with their attributes kept as the product defines them. Their starting values
    /// are added when missing and then belong to the entity (renamed, reordered or deactivated in change sets).
    /// </summary>
    /// <summary>
    /// Installations from before companies (ADR-0036) get, once, a company named after each organization with a head
    /// office as its default branch, so they keep working as a single company and branch. Later companies are added in
    /// Apps Control.
    /// </summary>
    private async Task SeedCompaniesAsync(CancellationToken cancellationToken)
    {
        if (await context.Companies.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var organization in await context.Entities.ToListAsync(cancellationToken))
        {
            var single = organization.Code == GovernmentEntity.SingletonCode;
            var company = single ? "MAIN" : $"{organization.Code}-MAIN";
            var branch = single ? "HQ" : $"{organization.Code}-HQ";
            if (await context.Companies.AnyAsync(c => c.Code == company, cancellationToken) || await context.Branches.AnyAsync(b => b.Code == branch, cancellationToken))
            {
                continue;
            }

            context.Companies.Add(new Company
            {
                Code = company,
                NameAr = organization.NameAr,
                NameEn = organization.NameEn,
                EntityCode = organization.Code,
                CrNumber = organization.CrNumber,
                UnifiedNumber = organization.UnifiedNumber,
            });
            context.Branches.Add(new Branch
            {
                Code = branch,
                NameAr = "المقر الرئيسي",
                NameEn = "Head office",
                EntityCode = organization.Code,
                CompanyCode = company,
                IsDefault = true,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedModuleClassesAsync(IReadOnlyList<CatalogClass> definitions, CancellationToken cancellationToken)
    {
        var classes = await UpsertAsync(context.Classes.Include(c => c.Attributes), definitions.Select(c => (c.Code, c.NameAr, c.NameEn, (Action<LookupClass>)(e =>
        {
            e.DescriptionAr = c.DescriptionAr;
            e.DescriptionEn = c.DescriptionEn;
        }))), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        foreach (var definition in definitions)
        {
            var cls = classes[definition.Code];
            foreach (var (attribute, i) in definition.Attributes.Select((a, i) => (a, i)))
            {
                var row = cls.Attributes.FirstOrDefault(a => a.Code == attribute.Code);
                if (row is null)
                {
                    row = new AttributeDefinition { ClassId = cls.Id, Code = attribute.Code };
                    context.AttributeDefinitions.Add(row);
                }

                row.NameAr = attribute.NameAr;
                row.NameEn = attribute.NameEn;
                row.DataType = attribute.DataType switch
                {
                    LookupAttributeTypes.Number => AttributeDataType.Number,
                    LookupAttributeTypes.Date => AttributeDataType.Date,
                    LookupAttributeTypes.Bool => AttributeDataType.Bool,
                    LookupAttributeTypes.DomainRef => AttributeDataType.DomainRef,
                    LookupAttributeTypes.Json => AttributeDataType.Json,
                    _ => AttributeDataType.Text,
                };
                row.RefClassCode = attribute.RefClass;
                row.IsRequired = attribute.Required;
                row.DisplayOrder = (i + 1) * 10;
            }

            var codes = definition.Values.Select(v => v.Code).ToList();
            var present = await context.Domains.Where(d => d.ClassId == cls.Id && codes.Contains(d.Code)).Select(d => d.Code).ToListAsync(cancellationToken);
            foreach (var (value, i) in definition.Values.Select((v, i) => (v, i)).Where(x => !present.Contains(x.v.Code)))
            {
                context.Domains.Add(new DomainValue
                {
                    ClassId = cls.Id,
                    Code = value.Code,
                    NameAr = value.NameAr,
                    NameEn = value.NameEn,
                    DisplayOrder = (i + 1) * 10,
                    Attributes = value.Attributes?.ToJsonString() ?? "{}",
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
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
