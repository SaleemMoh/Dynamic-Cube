using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

public sealed class OrgUnitTypePayload : ConfigPayload
{
    public int DisplayOrder { get; set; }

    public List<string> AllowedParentTypes { get; set; } = [];
}

internal sealed class OrgUnitTypeApplier(AppsControlDbContext db) : ConfigApplier<OrgUnitType, OrgUnitTypePayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.OrgUnitType;

    public override string Area => AppsControlAreas.Organization;

    public override string ResourceCode => AppsControlResources.Organization;

    protected override IQueryable<OrgUnitType> Query(AppsControlDbContext context) => context.OrgUnitTypes;

    protected override IQueryable<OrgUnitType> ListQuery(AppsControlDbContext context, string? parent) =>
        context.OrgUnitTypes.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Code);

    protected override async Task ApplyAsync(OrgUnitType entity, OrgUnitTypePayload payload, RefResolver refs, ValidationErrors errors, OrgUnitType? existing, CancellationToken cancellationToken)
    {
        var allowed = payload.AllowedParentTypes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).ToList();
        foreach (var code in allowed.Where(c => c != entity.Code))
        {
            await refs.OrgUnitTypeAsync(code, "allowedParentTypes", errors, cancellationToken);
        }

        entity.DisplayOrder = payload.DisplayOrder;
        entity.AllowedParentTypes = allowed;
    }

    protected override Task<OrgUnitTypePayload> ToPayloadAsync(AppsControlDbContext context, OrgUnitType entity, CancellationToken cancellationToken) =>
        Task.FromResult(new OrgUnitTypePayload { DisplayOrder = entity.DisplayOrder, AllowedParentTypes = entity.AllowedParentTypes });
}

public sealed class OrgUnitPayload : ConfigPayload
{
    public string TypeCode { get; set; } = string.Empty;

    public string? ParentCode { get; set; }

    public JsonObject? Attributes { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}

internal sealed class OrgUnitApplier(AppsControlDbContext db) : ConfigApplier<OrgUnit, OrgUnitPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.OrgUnit;

    public override string Area => AppsControlAreas.Organization;

    public override string ResourceCode => AppsControlResources.Organization;

    protected override IQueryable<OrgUnit> Query(AppsControlDbContext context) =>
        context.OrgUnits.Include(u => u.OrgUnitType).Include(u => u.Parent);

    protected override IQueryable<OrgUnit> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(u => u.Parent!.Code == parent)).OrderBy(u => u.Path);

    protected override async Task ApplyAsync(OrgUnit entity, OrgUnitPayload payload, RefResolver refs, ValidationErrors errors, OrgUnit? existing, CancellationToken cancellationToken)
    {
        var context = refs.Context;
        errors.Required("typeCode", payload.TypeCode);
        var typeId = await refs.OrgUnitTypeAsync(payload.TypeCode, "typeCode", errors, cancellationToken);
        var parentId = await refs.OrgUnitAsync(payload.ParentCode, "parentCode", errors, cancellationToken);
        if (payload.EffectiveTo < payload.EffectiveFrom)
        {
            errors.Add("effectiveTo", "Must not be before the start date.");
        }

        if (errors.HasErrors || typeId is null)
        {
            return;
        }

        var parent = parentId is { } pid && pid != Guid.Empty
            ? await context.OrgUnits.Include(u => u.OrgUnitType).SingleAsync(u => u.Id == pid, cancellationToken)
            : null;

        // Parent type constraint (skipped when the parent or the type is created earlier in the same draft).
        var type = typeId.Value == Guid.Empty ? null : await context.OrgUnitTypes.SingleAsync(t => t.Id == typeId, cancellationToken);
        if (type is { AllowedParentTypes.Count: > 0 } && parentId != Guid.Empty)
        {
            if (parent is null)
            {
                errors.Add("parentCode", $"A {type.Code} unit needs a parent of type {string.Join(", ", type.AllowedParentTypes)}.");
            }
            else if (!type.AllowedParentTypes.Contains(parent.OrgUnitType!.Code))
            {
                errors.Add("parentCode", $"A {type.Code} unit cannot be placed under a {parent.OrgUnitType!.Code} unit.");
            }
        }

        var self = existing ?? entity;
        var label = OrgUnit.PathLabel(self.Id);
        if (parent is not null && parent.Path.ToString().Split('.').Contains(label))
        {
            errors.Add("parentCode", "A unit cannot be placed under itself or one of its sub-units.");
        }

        if (errors.HasErrors)
        {
            return;
        }

        entity.OrgUnitTypeId = typeId.Value;
        entity.ParentId = parentId;
        entity.Attributes = (payload.Attributes ?? []).ToJsonString();
        entity.EffectiveFrom = payload.EffectiveFrom;
        entity.EffectiveTo = payload.EffectiveTo;

        if (refs.IsValidating)
        {
            return;
        }

        var newPath = new LTree(parent is null ? label : $"{parent.Path}.{label}");
        if (existing is not null && existing.Path != newPath)
        {
            // Move the subtree: descendants keep their relative path under the new location.
            var oldPath = existing.Path.ToString();
            var movedPath = newPath.ToString();
            await context.Database.ExecuteSqlAsync(
                $"UPDATE apps.org_unit SET path = {movedPath}::ltree || subpath(path, nlevel({oldPath}::ltree)) WHERE path <@ {oldPath}::ltree AND id <> {existing.Id}",
                cancellationToken);
        }

        entity.Path = newPath;
    }

    protected override Task<OrgUnitPayload> ToPayloadAsync(AppsControlDbContext context, OrgUnit entity, CancellationToken cancellationToken) =>
        Task.FromResult(new OrgUnitPayload
        {
            TypeCode = entity.OrgUnitType?.Code ?? string.Empty,
            ParentCode = entity.Parent?.Code,
            Attributes = JsonNode.Parse(entity.Attributes) as JsonObject,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
        });

    protected override async Task BeforeDeleteAsync(AppsControlDbContext context, OrgUnit entity, CancellationToken cancellationToken)
    {
        if (await context.OrgUnits.AnyAsync(u => u.ParentId == entity.Id, cancellationToken))
        {
            throw new DynamicCube.Platform.ChangeSets.ChangeSetException($"Org unit '{entity.Code}' has sub-units; move or delete them first.");
        }
    }
}

public sealed class PositionPayload : ConfigPayload;

internal sealed class PositionApplier(AppsControlDbContext db) : ConfigApplier<Position, PositionPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.Position;

    public override string Area => AppsControlAreas.Organization;

    public override string ResourceCode => AppsControlResources.Organization;

    protected override IQueryable<Position> Query(AppsControlDbContext context) => context.Positions;

    protected override Task ApplyAsync(Position entity, PositionPayload payload, RefResolver refs, ValidationErrors errors, Position? existing, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<PositionPayload> ToPayloadAsync(AppsControlDbContext context, Position entity, CancellationToken cancellationToken) =>
        Task.FromResult(new PositionPayload());
}

internal static class JsonNodeExtensions
{
    public static JsonNode? ParseOrNull(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

    public static string? ToJsonOrNull(this JsonNode? node) => node?.ToJsonString(JsonSerializerOptions.Default);
}
