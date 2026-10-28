using System.Text.Json;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>Apps Control metadata of an applier: the change-set area it belongs to and the resource that guards edits.</summary>
public interface IAppsControlApplier : IChangeApplier<AppsControlDbContext>
{
    /// <summary>Change-set area (maker-checker policy), e.g. <c>apps.security</c>.</summary>
    string Area { get; }

    /// <summary>Resource whose <c>EDIT</c> action is needed to add items of this type.</summary>
    string ResourceCode { get; }

    /// <summary>Current rows as payload snapshots (admin lists).</summary>
    Task<IReadOnlyList<JsonElement>> ListAsync(AppsControlDbContext context, string? parent, CancellationToken cancellationToken);

    /// <summary>One row as payload snapshot, or null.</summary>
    Task<JsonElement?> GetAsync(AppsControlDbContext context, string entityCode, CancellationToken cancellationToken);
}

/// <summary>
/// Common payload fields of a configuration row. <c>code</c> and <c>owner</c> are ignored on input: the key is the change
/// item's entity code, which the applier copies into <see cref="Code"/> before applying.
/// </summary>
public abstract class ConfigPayload
{
    public string? Code { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Ownership? Owner { get; set; }
}

/// <summary>
/// Base applier for a code-keyed configuration entity. Keys come from the change item's entity code; payloads carry the
/// remaining fields and refer to other rows by code, so change sets move between environments unchanged.
/// SYSTEM rows are maintained by product upgrades and cannot be changed or deleted through change sets.
/// </summary>
internal abstract class ConfigApplier<TEntity, TPayload> : IAppsControlApplier
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public abstract string EntityType { get; }

    public abstract string Area { get; }

    public abstract string ResourceCode { get; }

    protected virtual System.Text.RegularExpressions.Regex CodePattern => ValidationErrors.GeneralCode();

    protected virtual int NameMaxLength => 500;

    public async Task ValidateAsync(AppsControlDbContext context, ChangeSet changeSet, string entityCode, ChangeOperation operation, string? payload, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        ValidateKey(entityCode, errors);
        errors.ThrowIfAny();

        var existing = await FindAsync(context, entityCode, cancellationToken);
        var pending = changeSet.Items.Any(i => i.EntityType == EntityType && i.EntityCode == entityCode && i.Operation == ChangeOperation.Create);
        switch (operation)
        {
            case ChangeOperation.Create when existing is not null || pending:
                errors.Add("code", $"'{entityCode}' already exists.");
                break;
            case ChangeOperation.Update or ChangeOperation.Delete when existing is null && !pending:
                errors.Add("code", $"'{entityCode}' does not exist.");
                break;
            case ChangeOperation.Update or ChangeOperation.Delete when existing?.Owner == Ownership.System:
                errors.Add("code", $"'{entityCode}' is maintained by the product and cannot be changed.");
                break;
        }

        errors.ThrowIfAny();
        if (operation == ChangeOperation.Delete)
        {
            return;
        }

        var body = Deserialize(payload, errors);
        errors.ThrowIfAny();

        // Validate against a detached copy so nothing tracked is modified.
        var refs = new RefResolver(context, changeSet);
        var probe = new TEntity();
        await ApplyFieldsAsync(probe, entityCode, body!, refs, errors, existing, cancellationToken);
        errors.ThrowIfAny();
    }

    public async Task<string?> GetSnapshotAsync(AppsControlDbContext context, string entityCode, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(context, entityCode, cancellationToken);
        return entity is null ? null : ChangeJson.Serialize(await SnapshotAsync(context, entity, cancellationToken));
    }

    public async Task ApplyAsync(AppsControlDbContext context, ChangeItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(item);

        var errors = new ValidationErrors();
        var existing = await FindAsync(context, item.EntityCode, cancellationToken);
        if (item.Operation != ChangeOperation.Create && existing is null)
        {
            throw new ChangeSetException($"{EntityType} '{item.EntityCode}' no longer exists.");
        }

        if (existing?.Owner == Ownership.System)
        {
            throw new ChangeSetException($"{EntityType} '{item.EntityCode}' is maintained by the product and cannot be changed.");
        }

        switch (item.Operation)
        {
            case ChangeOperation.Create:
                if (existing is not null)
                {
                    throw new ChangeSetException($"{EntityType} '{item.EntityCode}' already exists.");
                }

                var created = new TEntity { Owner = Ownership.Entity };
                await ApplyFieldsAsync(created, item.EntityCode, Deserialize(item.After, errors)!, new RefResolver(context, null), errors, null, cancellationToken);
                errors.ThrowIfAny();
                context.Add(created);
                break;
            case ChangeOperation.Update:
                await ApplyFieldsAsync(existing!, item.EntityCode, Deserialize(item.After, errors)!, new RefResolver(context, null), errors, existing, cancellationToken);
                errors.ThrowIfAny();
                break;
            case ChangeOperation.Delete:
                await BeforeDeleteAsync(context, existing!, cancellationToken);
                context.Remove(existing!);
                break;
        }
    }

    public async Task<IReadOnlyList<JsonElement>> ListAsync(AppsControlDbContext context, string? parent, CancellationToken cancellationToken)
    {
        var rows = await ListQuery(context, parent).AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<JsonElement>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(JsonSerializer.SerializeToElement(await SnapshotAsync(context, row, cancellationToken), ChangeJson.Options));
        }

        return result;
    }

    public async Task<JsonElement?> GetAsync(AppsControlDbContext context, string entityCode, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(context, entityCode, cancellationToken);
        return entity is null ? null : JsonSerializer.SerializeToElement(await SnapshotAsync(context, entity, cancellationToken), ChangeJson.Options);
    }

    /// <summary>Query including the navigations needed by <see cref="ToPayloadAsync"/>.</summary>
    protected abstract IQueryable<TEntity> Query(AppsControlDbContext context);

    protected virtual IQueryable<TEntity> ListQuery(AppsControlDbContext context, string? parent) =>
        Query(context).OrderBy(e => e.Code);

    protected virtual Task<TEntity?> FindAsync(AppsControlDbContext context, string entityCode, CancellationToken cancellationToken) =>
        Query(context).SingleOrDefaultAsync(e => e.Code == entityCode, cancellationToken);

    protected virtual void ValidateKey(string entityCode, ValidationErrors errors) => errors.Code("code", entityCode, CodePattern);

    /// <summary>Copies payload fields into <paramref name="entity"/>, resolving references and reporting problems.</summary>
    protected abstract Task ApplyAsync(TEntity entity, TPayload payload, RefResolver refs, ValidationErrors errors, TEntity? existing, CancellationToken cancellationToken);

    protected abstract Task<TPayload> ToPayloadAsync(AppsControlDbContext context, TEntity entity, CancellationToken cancellationToken);

    protected virtual Task BeforeDeleteAsync(AppsControlDbContext context, TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The change-item key of a row; composite keys override this.</summary>
    protected virtual string EntityCodeOf(TEntity entity) => entity.Code;

    /// <summary>Sets the code from the entity code; composite keys override this.</summary>
    protected virtual string CodeFromKey(string entityCode) => entityCode;

    private async Task ApplyFieldsAsync(TEntity entity, string entityCode, TPayload payload, RefResolver refs, ValidationErrors errors, TEntity? existing, CancellationToken cancellationToken)
    {
        errors.Required("nameAr", payload.NameAr);
        errors.Required("nameEn", payload.NameEn);
        errors.MaxLength("nameAr", payload.NameAr, NameMaxLength);
        errors.MaxLength("nameEn", payload.NameEn, NameMaxLength);

        payload.Code = entityCode;
        entity.Code = CodeFromKey(entityCode);
        entity.NameAr = payload.NameAr?.Trim() ?? string.Empty;
        entity.NameEn = payload.NameEn?.Trim() ?? string.Empty;
        entity.IsActive = payload.IsActive;
        await ApplyAsync(entity, payload, refs, errors, existing, cancellationToken);
    }

    private async Task<TPayload> SnapshotAsync(AppsControlDbContext context, TEntity entity, CancellationToken cancellationToken)
    {
        var payload = await ToPayloadAsync(context, entity, cancellationToken);
        payload.Code = EntityCodeOf(entity);
        payload.NameAr = entity.NameAr;
        payload.NameEn = entity.NameEn;
        payload.IsActive = entity.IsActive;
        payload.Owner = entity.Owner;
        return payload;
    }

    private static TPayload? Deserialize(string? payload, ValidationErrors errors)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            errors.Add("payload", "Required.");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TPayload>(payload, ChangeJson.Options) ?? throw new JsonException("Empty payload.");
        }
        catch (JsonException ex)
        {
            errors.Add(string.IsNullOrEmpty(ex.Path) || ex.Path == "$" ? "payload" : ex.Path.TrimStart('$', '.'), "Invalid value.");
            return null;
        }
    }
}
