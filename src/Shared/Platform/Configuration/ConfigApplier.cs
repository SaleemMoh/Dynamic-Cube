using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Platform.Configuration;

/// <summary>
/// A configuration entity type editable through change sets, as seen by the admin API and the change-set inbox
/// (independent of the module that owns it).
/// </summary>
public interface IConfigApplier
{
    /// <summary>Entity type key, e.g. <c>apps.domain</c>, <c>integ.operation</c>.</summary>
    string EntityType { get; }

    /// <summary>Change-set area (maker-checker policy), e.g. <c>apps.security</c>.</summary>
    string Area { get; }

    /// <summary>Apps Control resource whose actions guard edits of this type.</summary>
    string ResourceCode { get; }

    /// <summary>Current rows as payload snapshots (admin lists).</summary>
    Task<IReadOnlyList<JsonElement>> ListAsync(string? parent, CancellationToken cancellationToken);

    /// <summary>One row as payload snapshot, or null.</summary>
    Task<JsonElement?> GetAsync(string entityCode, CancellationToken cancellationToken);
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
/// Resolves references by code. While validating a draft, rows created by earlier items of the same change set count
/// as existing (their id is not known yet, so <see cref="Guid.Empty"/> is returned).
/// </summary>
public class ConfigRefs<TContext>(TContext context, ChangeSet? draft)
    where TContext : ModuleDbContext
{
    public TContext Context => context;

    public bool IsValidating => draft is not null;

    /// <summary>The change set being checked (null when publishing): lets an item read another pending item's payload.</summary>
    public ChangeSet? Draft => draft;

    public bool IsPendingCreate(string entityType, string code) =>
        draft is not null && draft.Items.Any(i => i.EntityType == entityType && i.EntityCode == code && i.Operation == ChangeOperation.Create);

    /// <summary>Id of the row with <paramref name="code"/>, <see cref="Guid.Empty"/> for a pending create, or null (reported).</summary>
    public async Task<Guid?> ResolveAsync<T>(IQueryable<T> set, string entityType, string? code, string field, ValidationErrors errors, CancellationToken cancellationToken)
        where T : ConfigEntity
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var id = await set.Where(e => e.Code == code).Select(e => (Guid?)e.Id).SingleOrDefaultAsync(cancellationToken);
        if (id is null && IsPendingCreate(entityType, code))
        {
            return Guid.Empty;
        }

        if (id is null)
        {
            errors.Add(field, $"'{code}' does not exist.");
        }

        return id;
    }
}

/// <summary>
/// Base applier for a code-keyed configuration entity. Keys come from the change item's entity code; payloads carry the
/// remaining fields and refer to other rows by code, so change sets move between environments unchanged.
/// SYSTEM rows are maintained by product upgrades and cannot be changed or deleted through change sets.
/// </summary>
public abstract class ConfigApplier<TContext, TRefs, TEntity, TPayload>(TContext context) : IChangeApplier<TContext>, IConfigApplier
    where TContext : ModuleDbContext
    where TRefs : ConfigRefs<TContext>
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public abstract string EntityType { get; }

    public abstract string Area { get; }

    public abstract string ResourceCode { get; }

    protected TContext Context => context;

    protected virtual Regex CodePattern => ValidationErrors.GeneralCode();

    protected virtual int NameMaxLength => 500;

    public async Task ValidateAsync(TContext context, ChangeSet changeSet, string entityCode, ChangeOperation operation, string? payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changeSet);
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
        var probe = new TEntity();
        await ApplyFieldsAsync(probe, entityCode, body!, CreateRefs(context, changeSet), errors, existing, cancellationToken);
        errors.ThrowIfAny();
    }

    public async Task<string?> GetSnapshotAsync(TContext context, string entityCode, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(context, entityCode, cancellationToken);
        return entity is null ? null : ChangeJson.Serialize(await SnapshotAsync(context, entity, cancellationToken));
    }

    public async Task ApplyAsync(TContext context, ChangeItem item, CancellationToken cancellationToken)
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
                await ApplyFieldsAsync(created, item.EntityCode, Deserialize(item.After, errors)!, CreateRefs(context, null), errors, null, cancellationToken);
                errors.ThrowIfAny();
                context.Add(created);
                break;
            case ChangeOperation.Update:
                await ApplyFieldsAsync(existing!, item.EntityCode, Deserialize(item.After, errors)!, CreateRefs(context, null), errors, existing, cancellationToken);
                errors.ThrowIfAny();
                break;
            case ChangeOperation.Delete:
                await BeforeDeleteAsync(context, existing!, cancellationToken);
                context.Remove(existing!);
                break;
        }
    }

    public async Task<IReadOnlyList<JsonElement>> ListAsync(string? parent, CancellationToken cancellationToken)
    {
        var rows = await ListQuery(context, parent).AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<JsonElement>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(JsonSerializer.SerializeToElement(await SnapshotAsync(context, row, cancellationToken), ChangeJson.Options));
        }

        return result;
    }

    public async Task<JsonElement?> GetAsync(string entityCode, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(context, entityCode, cancellationToken);
        return entity is null ? null : JsonSerializer.SerializeToElement(await SnapshotAsync(context, entity, cancellationToken), ChangeJson.Options);
    }

    /// <summary>The reference resolver handed to <see cref="ApplyAsync(TEntity, TPayload, TRefs, ValidationErrors, TEntity, CancellationToken)"/>.</summary>
    protected abstract TRefs CreateRefs(TContext context, ChangeSet? draft);

    /// <summary>Query including the navigations needed by <see cref="ToPayloadAsync"/>.</summary>
    protected abstract IQueryable<TEntity> Query(TContext context);

    protected virtual IQueryable<TEntity> ListQuery(TContext context, string? parent) =>
        Query(context).OrderBy(e => e.Code);

    protected virtual Task<TEntity?> FindAsync(TContext context, string entityCode, CancellationToken cancellationToken) =>
        Query(context).SingleOrDefaultAsync(e => e.Code == entityCode, cancellationToken);

    protected virtual void ValidateKey(string entityCode, ValidationErrors errors) => errors.Code("code", entityCode, CodePattern);

    /// <summary>Copies payload fields into <paramref name="entity"/>, resolving references and reporting problems.</summary>
    protected abstract Task ApplyAsync(TEntity entity, TPayload payload, TRefs refs, ValidationErrors errors, TEntity? existing, CancellationToken cancellationToken);

    protected abstract Task<TPayload> ToPayloadAsync(TContext context, TEntity entity, CancellationToken cancellationToken);

    protected virtual Task BeforeDeleteAsync(TContext context, TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The change-item key of a row; composite keys override this.</summary>
    protected virtual string EntityCodeOf(TEntity entity) => entity.Code;

    /// <summary>Sets the code from the entity code; composite keys override this.</summary>
    protected virtual string CodeFromKey(string entityCode) => entityCode;

    private async Task ApplyFieldsAsync(TEntity entity, string entityCode, TPayload payload, TRefs refs, ValidationErrors errors, TEntity? existing, CancellationToken cancellationToken)
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

    private async Task<TPayload> SnapshotAsync(TContext context, TEntity entity, CancellationToken cancellationToken)
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
