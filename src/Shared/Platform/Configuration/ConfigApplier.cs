using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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

    /// <summary>
    /// Current rows the user may see as payload snapshots (admin lists); with <paramref name="businessApp"/>, only the rows
    /// of that app and those of no app.
    /// </summary>
    Task<IReadOnlyList<JsonElement>> ListAsync(string? parent, CancellationToken cancellationToken, string? businessApp = null);

    /// <summary>One row as payload snapshot, or null.</summary>
    Task<JsonElement?> GetAsync(string entityCode, CancellationToken cancellationToken);

    /// <summary>
    /// Every row as payload snapshots, in an order a package can apply them: parents before their children and versions
    /// in number order.
    /// </summary>
    Task<IReadOnlyList<JsonElement>> ExportAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A payload as this type reads it, shaped like a snapshot: unknown properties dropped and missing ones at their
    /// defaults, so it compares with <see cref="GetAsync"/>. Null when the payload does not parse.
    /// </summary>
    JsonElement? Normalize(string entityCode, string payload);
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

    /// <summary>
    /// The government entity the row belongs to. Null keeps the row's level (see <see cref="BusinessApp"/>); an empty string
    /// is the whole installation.
    /// </summary>
    public string? EntityCode { get; set; }

    /// <summary>
    /// The business app the row belongs to. When both are null the row keeps its level, and a new row takes the maker's
    /// own entity and app; an empty string is no app.
    /// </summary>
    public string? BusinessApp { get; set; }

    /// <summary>
    /// Every business app of the row when it serves several (the first is its own, the others shared); it replaces
    /// <see cref="BusinessApp"/> when given, and an empty list is no app. Null keeps the shared apps.
    /// </summary>
    public List<string>? BusinessApps { get; set; }
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

        // Staff change only the rows of their entities and business apps.
        var scope = await ScopeAsync(context, cancellationToken);
        if (existing is not null && !scope.CanChange(existing.EntityCode, existing.BusinessApp, existing.SharedApps))
        {
            errors.Add("code", $"'{entityCode}' belongs to an organization or app outside yours.");
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
        await ApplyFieldsAsync(probe, entityCode, body!, CreateRefs(context, changeSet), errors, existing, scope, cancellationToken);
        if (!errors.HasErrors && !scope.CanChange(probe.EntityCode, probe.BusinessApp, probe.SharedApps))
        {
            errors.Add(
                probe.BusinessApp is null ? "entityCode" : "businessApp",
                scope.Apps is not null && probe.BusinessApp is null
                    ? "Choose one of your apps for this row."
                    : "Choose one of your organizations and apps for this row.");
        }

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
        var maker = await MakerScopeAsync(context, item, cancellationToken);
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
                await ApplyFieldsAsync(created, item.EntityCode, Deserialize(item.After, errors)!, CreateRefs(context, null), errors, null, maker, cancellationToken);
                errors.ThrowIfAny();
                context.Add(created);
                break;
            case ChangeOperation.Update:
                await ApplyFieldsAsync(existing!, item.EntityCode, Deserialize(item.After, errors)!, CreateRefs(context, null), errors, existing, maker, cancellationToken);
                errors.ThrowIfAny();
                break;
            case ChangeOperation.Delete:
                await BeforeDeleteAsync(context, existing!, cancellationToken);
                context.Remove(existing!);
                break;
        }
    }

    public async Task<IReadOnlyList<JsonElement>> ListAsync(string? parent, CancellationToken cancellationToken, string? businessApp = null)
    {
        var scope = await ScopeAsync(context, cancellationToken);
        var rows = await ListQuery(context, parent).AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<JsonElement>(rows.Count);
        foreach (var row in rows.Where(r => scope.CanSee(r.EntityCode, r.BusinessApp, r.SharedApps) && (businessApp is null || InApp(r, businessApp))))
        {
            result.Add(JsonSerializer.SerializeToElement(await SnapshotAsync(context, row, cancellationToken), ChangeJson.Options));
        }

        return result;
    }

    public async Task<JsonElement?> GetAsync(string entityCode, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(context, entityCode, cancellationToken);
        if (entity is not null && !(await ScopeAsync(context, cancellationToken)).CanSee(entity.EntityCode, entity.BusinessApp, entity.SharedApps))
        {
            return null;
        }

        return entity is null ? null : JsonSerializer.SerializeToElement(await SnapshotAsync(context, entity, cancellationToken), ChangeJson.Options);
    }

    public async Task<IReadOnlyList<JsonElement>> ExportAsync(CancellationToken cancellationToken)
    {
        var scope = await ScopeAsync(context, cancellationToken);
        var rows = (await ExportQuery(context).AsNoTracking().ToListAsync(cancellationToken)).Where(r => scope.CanSee(r.EntityCode, r.BusinessApp, r.SharedApps)).ToList();
        var result = new List<JsonElement>(rows.Count);
        foreach (var row in ParentsFirst(rows))
        {
            result.Add(JsonSerializer.SerializeToElement(await SnapshotAsync(context, row, cancellationToken), ChangeJson.Options));
        }

        return result;
    }

    public JsonElement? Normalize(string entityCode, string payload)
    {
        try
        {
            var body = JsonSerializer.Deserialize<TPayload>(payload, ChangeJson.Options);
            if (body is null)
            {
                return null;
            }

            body.Code = entityCode;
            return JsonSerializer.SerializeToElement(body, ChangeJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The reference resolver handed to <see cref="ApplyAsync(TEntity, TPayload, TRefs, ValidationErrors, TEntity, CancellationToken)"/>.</summary>
    protected abstract TRefs CreateRefs(TContext context, ChangeSet? draft);

    /// <summary>Query including the navigations needed by <see cref="ToPayloadAsync"/>.</summary>
    protected abstract IQueryable<TEntity> Query(TContext context);

    protected virtual IQueryable<TEntity> ListQuery(TContext context, string? parent) =>
        Query(context).OrderBy(e => e.Code);

    /// <summary>Rows in export order within the type; versions override this to go up by number.</summary>
    protected virtual IQueryable<TEntity> ExportQuery(TContext context) => ListQuery(context, null);

    /// <summary>The row of the same type this row hangs under, for types that form a tree.</summary>
    protected virtual Guid? ParentOf(TEntity entity) => null;

    protected virtual Task<TEntity?> FindAsync(TContext context, string entityCode, CancellationToken cancellationToken) =>
        Query(context).SingleOrDefaultAsync(e => e.Code == entityCode, cancellationToken);

    protected virtual void ValidateKey(string entityCode, ValidationErrors errors) => errors.Code("code", entityCode, CodePattern);

    /// <summary>Copies payload fields into <paramref name="entity"/>, resolving references and reporting problems.</summary>
    protected abstract Task ApplyAsync(TEntity entity, TPayload payload, TRefs refs, ValidationErrors errors, TEntity? existing, CancellationToken cancellationToken);

    protected abstract Task<TPayload> ToPayloadAsync(TContext context, TEntity entity, CancellationToken cancellationToken);

    protected virtual Task BeforeDeleteAsync(TContext context, TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Whether a row is listed under a business app: its own app, one it is shared with, or none. Types linked to apps
    /// otherwise (users through their roles) override this.
    /// </summary>
    protected virtual bool InApp(TEntity entity, string businessApp) => entity.BusinessApp is null || entity.BusinessApp == businessApp || entity.SharedApps.Contains(businessApp);

    /// <summary>The change-item key of a row; composite keys override this.</summary>
    protected virtual string EntityCodeOf(TEntity entity) => entity.Code;

    /// <summary>Sets the code from the entity code; composite keys override this.</summary>
    protected virtual string CodeFromKey(string entityCode) => entityCode;

    private async Task ApplyFieldsAsync(
        TEntity entity, string entityCode, TPayload payload, TRefs refs, ValidationErrors errors, TEntity? existing, ConfigScope maker, CancellationToken cancellationToken)
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
        // A payload without a level keeps the row's (a new row takes the maker's own entity and app when they have one
        // each); an empty string sets it to none.
        var scopes = ScopesOf(refs.Context);
        string? entityCodeIn, appIn;
        List<string> sharedIn;
        if (payload.EntityCode is null && payload.BusinessApp is null && payload.BusinessApps is null)
        {
            entityCodeIn = existing is not null ? existing.EntityCode : maker.Entities is { Count: 1 } entities ? entities.First() : null;
            appIn = existing is not null ? existing.BusinessApp : maker.Apps is { Count: 1 } apps ? apps.First() : null;
            sharedIn = existing?.SharedApps ?? [];
        }
        else
        {
            entityCodeIn = string.IsNullOrWhiteSpace(payload.EntityCode) ? null : payload.EntityCode.Trim();
            if (payload.BusinessApps is not null)
            {
                // Several apps: the first is the row's own, the others shared.
                var apps = payload.BusinessApps.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct(StringComparer.Ordinal).ToList();
                appIn = apps.FirstOrDefault();
                sharedIn = [.. apps.Skip(1)];
            }
            else
            {
                appIn = string.IsNullOrWhiteSpace(payload.BusinessApp) ? null : payload.BusinessApp.Trim();
                sharedIn = appIn is null ? [] : [.. (existing?.SharedApps ?? []).Where(a => a != appIn)];
            }
        }

        (entity.EntityCode, entity.BusinessApp) = scopes is not null
            ? await scopes.ResolveAsync(entityCodeIn, appIn, errors, cancellationToken, refs.Draft)
            : (entityCodeIn, appIn);
        // Shared apps must exist and belong to the row's entity.
        foreach (var shared in sharedIn)
        {
            if (scopes is not null && entity.BusinessApp is not null)
            {
                var (sharedEntity, _) = await scopes.ResolveAsync(entity.EntityCode, shared, errors, cancellationToken, refs.Draft);
                if (!errors.HasErrors && sharedEntity != entity.EntityCode)
                {
                    errors.Add("businessApps", $"App '{shared}' belongs to another organization.");
                }
            }
        }

        entity.SharedApps = entity.BusinessApp is null ? [] : sharedIn;
        await ApplyAsync(entity, payload, refs, errors, existing, cancellationToken);
    }

    /// <summary>The entities and apps of the signed-in user (everything when Apps Control is not installed or outside a request).</summary>
    private static async Task<ConfigScope> ScopeAsync(TContext context, CancellationToken cancellationToken) =>
        ScopesOf(context) is { } scopes ? await scopes.CurrentAsync(cancellationToken) : ConfigScope.Everything;

    /// <summary>The scope of whoever made the change set (a new row without a level takes theirs).</summary>
    private static async Task<ConfigScope> MakerScopeAsync(TContext context, ChangeItem item, CancellationToken cancellationToken)
    {
        if (ScopesOf(context) is not { } scopes)
        {
            return ConfigScope.Everything;
        }

        var maker = await context.ChangeSets.AsNoTracking().Where(c => c.Id == item.ChangeSetId).Select(c => c.CreatedBy).SingleOrDefaultAsync(cancellationToken);
        return maker is null ? ConfigScope.Everything : await scopes.OfUserAsync(maker, cancellationToken);
    }

    private static IConfigScopes? ScopesOf(DbContext context)
    {
        try
        {
            return context.GetService<IConfigScopes>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<TPayload> SnapshotAsync(TContext context, TEntity entity, CancellationToken cancellationToken)
    {
        var payload = await ToPayloadAsync(context, entity, cancellationToken);
        payload.Code = EntityCodeOf(entity);
        payload.NameAr = entity.NameAr;
        payload.NameEn = entity.NameEn;
        payload.IsActive = entity.IsActive;
        payload.Owner = entity.Owner;
        payload.EntityCode = entity.EntityCode;
        payload.BusinessApp = entity.BusinessApp;
        payload.BusinessApps = [.. entity.AllApps];
        return payload;
    }

    /// <summary>Keeps the query order but moves every child after its parent.</summary>
    private List<TEntity> ParentsFirst(List<TEntity> rows)
    {
        var byId = rows.ToDictionary(r => r.Id);
        var placed = new HashSet<Guid>();
        var ordered = new List<TEntity>(rows.Count);
        void Place(TEntity row, int depth)
        {
            if (placed.Contains(row.Id))
            {
                return;
            }

            // A cycle cannot be stored, but stop rather than loop if one is.
            if (depth < rows.Count && ParentOf(row) is { } parentId && byId.TryGetValue(parentId, out var parent))
            {
                Place(parent, depth + 1);
            }

            if (placed.Add(row.Id))
            {
                ordered.Add(row);
            }
        }

        foreach (var row in rows)
        {
            Place(row, 0);
        }

        return ordered;
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
