using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Resources other modules create at run time (ADR-0045), saved with the product's ownership and kept in line.</summary>
internal sealed class ResourceRegistry(AppsControlDbContext context, AccessCache cache) : IResourceRegistry
{
    public async Task SaveAsync(CatalogResource resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var app = await context.Apps.SingleOrDefaultAsync(a => a.Code == resource.App, cancellationToken)
            ?? throw new InvalidOperationException($"App '{resource.App}' does not exist.");
        var parent = resource.Parent is null ? null : await context.Resources.SingleOrDefaultAsync(r => r.Code == resource.Parent, cancellationToken)
            ?? throw new InvalidOperationException($"Resource '{resource.Parent}' does not exist.");
        var entity = await context.Resources.SingleOrDefaultAsync(r => r.Code == resource.Code, cancellationToken);
        var created = entity is null;
        if (entity is null)
        {
            entity = new Resource { Code = resource.Code };
            context.Resources.Add(entity);
        }

        entity.Owner = Ownership.System;
        entity.NameAr = resource.NameAr;
        entity.NameEn = resource.NameEn;
        entity.IsActive = true;
        entity.AppId = app.Id;
        entity.ParentId = parent?.Id;
        entity.ResourceType = Enum.Parse<ResourceType>(resource.Type, ignoreCase: true);
        entity.AvailableActions = [.. resource.Actions];

        // The system administrator holds every action of a new resource, as of the product's own; other roles are granted it.
        if (created && await context.Roles.SingleOrDefaultAsync(r => r.Code == BuiltInRoles.SystemAdmin, cancellationToken) is { } admin)
        {
            context.Privileges.Add(new Privilege { RoleId = admin.Id, ResourceId = entity.Id, Actions = [.. resource.Actions] });
        }

        await context.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
    }

    public async Task RetireAsync(string code, CancellationToken cancellationToken)
    {
        var entity = await context.Resources.SingleOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (entity is { IsActive: true })
        {
            entity.IsActive = false;
            await context.SaveChangesAsync(cancellationToken);
            cache.Invalidate();
        }
    }
}
