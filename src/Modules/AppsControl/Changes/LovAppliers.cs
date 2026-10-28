using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.AppsControl.Changes;

public sealed class DataObjectColumnPayload
{
    public string Name { get; set; } = string.Empty;

    public string? DataType { get; set; }

    public string? LabelAr { get; set; }

    public string? LabelEn { get; set; }

    public bool Filterable { get; set; }

    public bool Searchable { get; set; }
}

public sealed class ScopeBindingPayload
{
    public string ColumnName { get; set; } = string.Empty;

    public string LovCode { get; set; } = string.Empty;

    public List<string> AppliesTo { get; set; } = [];
}

public sealed class DataObjectPayload : ConfigPayload
{
    public string DbSchema { get; set; } = string.Empty;

    public string DbObject { get; set; } = string.Empty;

    public DataObjectType ObjectType { get; set; } = DataObjectType.View;

    public List<DataObjectColumnPayload> Columns { get; set; } = [];

    public List<ScopeBindingPayload> ScopeBindings { get; set; } = [];
}

internal sealed class DataObjectApplier(IOptions<AppsControlOptions> options) : ConfigApplier<DataObject, DataObjectPayload>
{
    public override string EntityType => AppsControlChangeTypes.DataObject;

    public override string Area => AppsControlAreas.Lovs;

    public override string ResourceCode => AppsControlResources.Lovs;

    protected override IQueryable<DataObject> Query(AppsControlDbContext context) =>
        context.DataObjects.Include(o => o.ScopeBindings).ThenInclude(b => b.Lov);

    protected override async Task ApplyAsync(DataObject entity, DataObjectPayload payload, RefResolver refs, ValidationErrors errors, DataObject? existing, CancellationToken cancellationToken)
    {
        errors.Code("dbSchema", payload.DbSchema, ValidationErrors.SqlIdentifier());
        errors.Code("dbObject", payload.DbObject, ValidationErrors.SqlIdentifier());
        if (!options.Value.DataObjectSchemas.Contains(payload.DbSchema, StringComparer.Ordinal))
        {
            errors.Add("dbSchema", $"Schema '{payload.DbSchema}' is not open for data objects.");
        }

        if (errors.HasErrors)
        {
            return;
        }

        var dbColumns = await DatabaseCatalog.GetColumnsAsync(refs.Context, payload.DbSchema, payload.DbObject, cancellationToken);
        if (dbColumns.Count == 0)
        {
            errors.Add("dbObject", $"{payload.DbSchema}.{payload.DbObject} does not exist.");
            return;
        }

        ChildSync.RequireUnique(payload.Columns, c => c.Name, "columns", errors);
        if (payload.Columns.Count == 0)
        {
            errors.Add("columns", "Register at least one column.");
        }

        foreach (var (c, i) in payload.Columns.Select((c, i) => (c, i)))
        {
            if (!dbColumns.ContainsKey(c.Name ?? string.Empty))
            {
                errors.Add($"columns[{i}].name", $"Column '{c.Name}' does not exist.");
            }
        }

        var registered = payload.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        ChildSync.RequireUnique(payload.ScopeBindings, b => b.ColumnName, "scopeBindings", errors);
        var bindings = new List<DataScopeBinding>();
        foreach (var (b, i) in payload.ScopeBindings.Select((b, i) => (b, i)))
        {
            if (!registered.Contains(b.ColumnName))
            {
                errors.Add($"scopeBindings[{i}].columnName", $"Column '{b.ColumnName}' is not registered.");
            }

            var lovId = await refs.LovAsync(b.LovCode, $"scopeBindings[{i}].lovCode", errors, cancellationToken);
            bindings.Add(new DataScopeBinding { DataObjectId = entity.Id, ColumnName = b.ColumnName, LovId = lovId ?? Guid.Empty, AppliesTo = b.AppliesTo.Distinct().ToList() });
        }

        entity.DbSchema = payload.DbSchema;
        entity.DbObject = payload.DbObject;
        entity.ObjectType = payload.ObjectType;
        entity.Columns = payload.Columns
            .Select(c => new DataObjectColumn
            {
                Name = c.Name,
                DataType = string.IsNullOrWhiteSpace(c.DataType) && dbColumns.TryGetValue(c.Name, out var dbType) ? dbType : c.DataType ?? "TEXT",
                LabelAr = c.LabelAr,
                LabelEn = c.LabelEn,
                Filterable = c.Filterable,
                Searchable = c.Searchable,
            })
            .ToList();

        if (errors.HasErrors || refs.IsValidating)
        {
            return;
        }

        ChildSync.Sync(entity.ScopeBindings, bindings, b => b.ColumnName, (from, to) =>
        {
            to.LovId = from.LovId;
            to.AppliesTo = from.AppliesTo;
        });
    }

    protected override Task<DataObjectPayload> ToPayloadAsync(AppsControlDbContext context, DataObject entity, CancellationToken cancellationToken) =>
        Task.FromResult(new DataObjectPayload
        {
            DbSchema = entity.DbSchema,
            DbObject = entity.DbObject,
            ObjectType = entity.ObjectType,
            Columns = entity.Columns
                .Select(c => new DataObjectColumnPayload { Name = c.Name, DataType = c.DataType, LabelAr = c.LabelAr, LabelEn = c.LabelEn, Filterable = c.Filterable, Searchable = c.Searchable })
                .ToList(),
            ScopeBindings = entity.ScopeBindings
                .OrderBy(b => b.ColumnName)
                .Select(b => new ScopeBindingPayload { ColumnName = b.ColumnName, LovCode = b.Lov?.Code ?? string.Empty, AppliesTo = b.AppliesTo })
                .ToList(),
        });
}

public sealed class LovBindingPayload
{
    public string ResourceCode { get; set; } = string.Empty;

    public string FieldCode { get; set; } = string.Empty;

    public SelectionMode SelectionMode { get; set; }
}

public sealed class LovPayload : ConfigPayload
{
    public LovSourceType SourceType { get; set; }

    public string? ClassCode { get; set; }

    public string? DataObjectCode { get; set; }

    public string? IntegrationOperationCode { get; set; }

    public string? SqlText { get; set; }

    public string? ValueColumn { get; set; }

    public string? DisplayArColumn { get; set; }

    public string? DisplayEnColumn { get; set; }

    public string? ParentColumn { get; set; }

    public JsonNode? StaticFilter { get; set; }

    public SelectionMode DefaultSelectionMode { get; set; }

    public LovSearchMode SearchMode { get; set; }

    public int MaxRows { get; set; } = 500;

    public bool RequiresGrant { get; set; }

    public bool IsPublic { get; set; }

    public List<LovBindingPayload> Bindings { get; set; } = [];
}

internal sealed class LovApplier(JsonLogicEvaluator evaluator, SqlLovRunner sqlRunner) : ConfigApplier<Lov, LovPayload>
{
    public const int MaxRowsLimit = 5000;

    public override string EntityType => AppsControlChangeTypes.Lov;

    public override string Area => AppsControlAreas.Lovs;

    public override string ResourceCode => AppsControlResources.Lovs;

    protected override IQueryable<Lov> Query(AppsControlDbContext context) =>
        context.Lovs.Include(l => l.Class).Include(l => l.DataObject).Include(l => l.Bindings).ThenInclude(b => b.Resource);

    protected override async Task ApplyAsync(Lov entity, LovPayload payload, RefResolver refs, ValidationErrors errors, Lov? existing, CancellationToken cancellationToken)
    {
        entity.SourceType = payload.SourceType;
        entity.ClassId = null;
        entity.DataObjectId = null;
        entity.IntegrationOperationCode = null;
        entity.SqlText = null;
        entity.ValueColumn = payload.ValueColumn;
        entity.DisplayArColumn = payload.DisplayArColumn;
        entity.DisplayEnColumn = payload.DisplayEnColumn;
        entity.ParentColumn = payload.ParentColumn;

        switch (payload.SourceType)
        {
            case LovSourceType.Class:
                errors.Required("classCode", payload.ClassCode);
                entity.ClassId = await refs.ClassAsync(payload.ClassCode, "classCode", errors, cancellationToken);
                entity.ValueColumn = entity.DisplayArColumn = entity.DisplayEnColumn = entity.ParentColumn = null;
                break;
            case LovSourceType.DataObject:
                errors.Required("dataObjectCode", payload.DataObjectCode);
                entity.DataObjectId = await refs.DataObjectAsync(payload.DataObjectCode, "dataObjectCode", errors, cancellationToken);
                await ValidateColumnsAsync(refs, entity.DataObjectId, payload, errors, cancellationToken);
                break;
            case LovSourceType.Sql:
                errors.Required("sqlText", payload.SqlText);
                errors.MaxLength("sqlText", payload.SqlText, 8000);
                if (!sqlRunner.IsEnabled)
                {
                    errors.Add("sourceType", "Restricted SQL is not enabled on this installation.");
                }
                else if (!string.IsNullOrWhiteSpace(payload.SqlText))
                {
                    var problem = await sqlRunner.CheckAsync(payload.SqlText, payload.ParentColumn, cancellationToken);
                    if (problem is not null)
                    {
                        errors.Add("sqlText", problem);
                    }
                }

                entity.SqlText = payload.SqlText;
                entity.ValueColumn = entity.DisplayArColumn = entity.DisplayEnColumn = null;
                break;
            case LovSourceType.Integration:
                errors.Add("sourceType", "Integration LOVs become available with the Integration Manager.");
                entity.IntegrationOperationCode = payload.IntegrationOperationCode;
                break;
        }

        if (payload.StaticFilter is not null && payload.StaticFilter.GetValueKind() != JsonValueKind.Null)
        {
            try
            {
                evaluator.Evaluate(payload.StaticFilter, new JsonObject { ["code"] = "X", ["parent"] = null, ["attributes"] = new JsonObject(), ["user"] = new JsonObject() });
            }
            catch (JsonLogicException ex)
            {
                errors.Add("staticFilter", ex.Message);
            }

            if (payload.SourceType != LovSourceType.Class)
            {
                errors.Add("staticFilter", "Static filters apply to class LOVs; filter data objects and SQL with a view.");
            }
        }

        if (payload.MaxRows is < 1 or > MaxRowsLimit)
        {
            errors.Add("maxRows", $"Use 1 to {MaxRowsLimit}.");
        }

        entity.StaticFilter = payload.StaticFilter?.GetValueKind() is null or JsonValueKind.Null ? null : payload.StaticFilter.ToJsonString();
        entity.DefaultSelectionMode = payload.DefaultSelectionMode;
        entity.SearchMode = payload.SearchMode;
        entity.MaxRows = payload.MaxRows;
        entity.RequiresGrant = payload.RequiresGrant;
        entity.IsPublic = payload.IsPublic;

        ChildSync.RequireUnique(payload.Bindings, b => (b.ResourceCode, b.FieldCode), "bindings", errors);
        var bindings = new List<LovBinding>();
        foreach (var (b, i) in payload.Bindings.Select((b, i) => (b, i)))
        {
            errors.Required($"bindings[{i}].resourceCode", b.ResourceCode);
            errors.Code($"bindings[{i}].fieldCode", b.FieldCode, ValidationErrors.GeneralCode());
            var resourceId = await refs.ResourceAsync(b.ResourceCode, $"bindings[{i}].resourceCode", errors, cancellationToken);
            bindings.Add(new LovBinding { LovId = entity.Id, ResourceId = resourceId ?? Guid.Empty, FieldCode = b.FieldCode, SelectionMode = b.SelectionMode });
        }

        if (errors.HasErrors || refs.IsValidating)
        {
            return;
        }

        ChildSync.Sync(entity.Bindings, bindings, b => (b.ResourceId, b.FieldCode), (from, to) => to.SelectionMode = from.SelectionMode);
    }

    protected override Task<LovPayload> ToPayloadAsync(AppsControlDbContext context, Lov entity, CancellationToken cancellationToken) =>
        Task.FromResult(new LovPayload
        {
            SourceType = entity.SourceType,
            ClassCode = entity.Class?.Code,
            DataObjectCode = entity.DataObject?.Code,
            IntegrationOperationCode = entity.IntegrationOperationCode,
            SqlText = entity.SqlText,
            ValueColumn = entity.ValueColumn,
            DisplayArColumn = entity.DisplayArColumn,
            DisplayEnColumn = entity.DisplayEnColumn,
            ParentColumn = entity.ParentColumn,
            StaticFilter = JsonNodeExtensions.ParseOrNull(entity.StaticFilter),
            DefaultSelectionMode = entity.DefaultSelectionMode,
            SearchMode = entity.SearchMode,
            MaxRows = entity.MaxRows,
            RequiresGrant = entity.RequiresGrant,
            IsPublic = entity.IsPublic,
            Bindings = entity.Bindings
                .OrderBy(b => b.Resource?.Code).ThenBy(b => b.FieldCode)
                .Select(b => new LovBindingPayload { ResourceCode = b.Resource?.Code ?? string.Empty, FieldCode = b.FieldCode, SelectionMode = b.SelectionMode })
                .ToList(),
        });

    protected override async Task BeforeDeleteAsync(AppsControlDbContext context, Lov entity, CancellationToken cancellationToken)
    {
        if (await context.DataScopeBindings.AnyAsync(b => b.LovId == entity.Id, cancellationToken))
        {
            throw new DynamicCube.Platform.ChangeSets.ChangeSetException($"LOV '{entity.Code}' is used by data scope bindings.");
        }
    }

    private static async Task ValidateColumnsAsync(RefResolver refs, Guid? dataObjectId, LovPayload payload, ValidationErrors errors, CancellationToken cancellationToken)
    {
        errors.Required("valueColumn", payload.ValueColumn);
        errors.Required("displayArColumn", payload.DisplayArColumn);
        errors.Required("displayEnColumn", payload.DisplayEnColumn);
        if (dataObjectId is not { } id || id == Guid.Empty)
        {
            return;
        }

        var columns = await refs.Context.DataObjects.Where(o => o.Id == id).Select(o => o.Columns).SingleAsync(cancellationToken);
        var names = columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (field, value) in new[] { ("valueColumn", payload.ValueColumn), ("displayArColumn", payload.DisplayArColumn), ("displayEnColumn", payload.DisplayEnColumn), ("parentColumn", payload.ParentColumn) })
        {
            if (!string.IsNullOrWhiteSpace(value) && !names.Contains(value))
            {
                errors.Add(field, $"'{value}' is not a registered column of the data object.");
            }
        }
    }
}
