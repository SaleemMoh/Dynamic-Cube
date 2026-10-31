using System.Data;
using System.Data.Common;
using System.Text.Json;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Lookups;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>What a preview shows: the SQL the definition becomes, the first values, and what is wrong with it.</summary>
public sealed record LovQueryPreview(string? Sql, IReadOnlyList<LovItem> Items, bool HasMore, IReadOnlyList<LovQueryProblem> Problems, string? Error);

/// <summary>
/// Sources of the query builder (registered data objects and classes, with the columns they offer) and the runs of
/// query LOVs. Queries run on a read-only transaction with a statement timeout; the data object's data scopes apply.
/// </summary>
public sealed class LovQueryService(AppsControlDbContext context, DataScopeService scopes)
{
    public const int TimeoutMilliseconds = 5000;

    /// <summary>Fixed columns of a class view, in the order the builder offers them.</summary>
    private static readonly (string Name, string Type, string Ar, string En)[] ClassColumns =
    [
        ("code", "TEXT", "الرمز", "Code"),
        ("name_ar", "TEXT", "الاسم بالعربية", "Arabic name"),
        ("name_en", "TEXT", "الاسم بالإنجليزية", "English name"),
        ("parent_code", "TEXT", "رمز القيمة الأعلى", "Parent code"),
        ("details_ar", "TEXT", "التفاصيل بالعربية", "Arabic details"),
        ("details_en", "TEXT", "التفاصيل بالإنجليزية", "English details"),
        ("display_order", "INTEGER", "ترتيب العرض", "Display order"),
        ("is_default", "BOOLEAN", "افتراضية", "Default"),
        ("effective_from", "DATE", "سارية من", "Effective from"),
        ("effective_to", "DATE", "سارية حتى", "Effective to"),
    ];

    private Dictionary<(string Kind, string Code), LovQuerySourceInfo>? _sources;

    /// <summary>Every source the builder may use: active data objects (their registered columns) and classes.</summary>
    public async Task<IReadOnlyList<LovQuerySourceInfo>> SourcesAsync(CancellationToken cancellationToken) =>
        [.. (await LoadAsync(cancellationToken)).Values.OrderBy(s => s.Kind, StringComparer.Ordinal).ThenBy(s => s.Code, StringComparer.Ordinal)];

    /// <param name="pending">Sources registered earlier in the same change set (not saved yet), which count as existing.</param>
    public async Task<IReadOnlyList<LovQueryProblem>> CheckAsync(LovQueryDefinition? query, IEnumerable<LovQuerySourceInfo>? pending, CancellationToken cancellationToken)
    {
        var sources = new Dictionary<(string Kind, string Code), LovQuerySourceInfo>(await LoadAsync(cancellationToken));
        foreach (var source in pending ?? [])
        {
            sources[(source.Kind, source.Code)] = source;
        }

        return LovQueryCompiler.Check(query, (kind, code) => sources.GetValueOrDefault((kind, code)));
    }

    /// <summary>A data object as a source, from what is registered (its columns' types read from the database when not given).</summary>
    internal static LovQuerySourceInfo DataObjectSource(string code, string nameAr, string nameEn, string schema, string dbObject, IEnumerable<DataObjectColumn> columns) =>
        new(LovQueryKinds.DataObject, code, nameAr, nameEn, schema, dbObject,
            columns.Select(c => new LovSourceColumn(c.Name, c.DataType, LovQueryCompiler.CategoryOf(c.DataType), c.LabelAr, c.LabelEn)).ToList());

    /// <summary>Values of a query LOV for the user: their grant on the value, their data scopes, the parent and search.</summary>
    public async Task<List<LovItem>> QueryAsync(Lov lov, AccessSnapshot access, string? parent, string? search, GrantSet grant, int limit, int offset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lov);
        ArgumentNullException.ThrowIfNull(access);
        var query = Parse(lov.Query);
        var (compiled, problems) = await CompileAsync(query, access, new LovQueryRun(Facts(access), parent, search, grant, limit, offset), null, cancellationToken);
        if (compiled is null)
        {
            // A source or column removed after the list was saved: the list shows nothing rather than failing the screen.
            return [];
        }

        return await RunAsync(compiled, cancellationToken);
    }

    /// <summary>Runs a definition as the administrator would see it, without grants, to try it while building.</summary>
    public async Task<LovQueryPreview> PreviewAsync(LovQueryDefinition? query, AccessSnapshot access, int limit, string? search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);
        limit = Math.Clamp(limit, 1, 200);
        var (compiled, problems) = await CompileAsync(query, access, new LovQueryRun(Facts(access), null, search, null, limit + 1, 0), null, cancellationToken);
        if (compiled is null)
        {
            return new LovQueryPreview(null, [], false, problems, null);
        }

        try
        {
            var rows = await RunAsync(compiled, cancellationToken);
            return new LovQueryPreview(compiled.Sql, rows.Take(limit).ToList(), rows.Count > limit, [], null);
        }
        catch (DbException ex)
        {
            return new LovQueryPreview(compiled.Sql, [], false, [], ex.Message);
        }
    }

    internal static LovQueryDefinition? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<LovQueryDefinition>(json, ChangeJson.Options);

    internal static LovUserFacts Facts(AccessSnapshot access) => new(
        access.Company,
        access.Branches,
        access.User.PrimaryOrgUnit,
        access.User.OrgUnits,
        access.User.SubtreeOrgUnits,
        access.User.UserName,
        access.User.App,
        access.User.Roles);

    private async Task<(LovCompiledQuery? Query, IReadOnlyList<LovQueryProblem> Problems)> CompileAsync(
        LovQueryDefinition? query, AccessSnapshot access, LovQueryRun run, string? resource, CancellationToken cancellationToken)
    {
        var sources = await LoadAsync(cancellationToken);

        // Data scopes of the data objects read (the same rule filters lists, inboxes and reports).
        var scoped = new Dictionary<string, IReadOnlyList<DataScopeFilter>>(StringComparer.Ordinal);
        if (query is not null)
        {
            foreach (var source in new LovQuerySource[] { query.From }.Concat(query.Joins).Where(s => s.Kind == LovQueryKinds.DataObject && !string.IsNullOrEmpty(s.Alias)))
            {
                var filters = await scopes.GetFiltersAsync(source.Code, access, resource, cancellationToken);
                if (filters.Count > 0)
                {
                    scoped[source.Alias] = filters;
                }
            }
        }

        return LovQueryCompiler.Compile(query, (kind, code) => sources.GetValueOrDefault((kind, code)), run with { Scopes = scoped });
    }

    private async Task<List<LovItem>> RunAsync(LovCompiledQuery compiled, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var ambient = context.Database.CurrentTransaction?.GetDbTransaction();
        var transaction = ambient ?? await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (ambient is null)
            {
                await using var setup = connection.CreateCommand();
                setup.Transaction = transaction;
                setup.CommandText = $"SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = {TimeoutMilliseconds}";
                await setup.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
#pragma warning disable CA2100 // Built by LovQueryCompiler from quoted registered identifiers; every value is a bound parameter.
            command.CommandText = compiled.Sql;
#pragma warning restore CA2100
            foreach (var (name, value) in compiled.Parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }

            var items = new List<LovItem>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new LovItem(
                        reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3)));
                }
            }

            return items;
        }
        finally
        {
            if (ambient is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await transaction.DisposeAsync();
            }

            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<Dictionary<(string Kind, string Code), LovQuerySourceInfo>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_sources is not null)
        {
            return _sources;
        }

        var sources = new Dictionary<(string, string), LovQuerySourceInfo>();

        // Saved data objects, and those added in this unit of work (an item earlier in the change set being published).
        var dataObjects = await context.DataObjects.AsNoTracking().Where(o => o.IsActive).ToListAsync(cancellationToken);
        foreach (var o in dataObjects.Concat(context.DataObjects.Local.Where(o => o.IsActive)))
        {
            sources[(LovQueryKinds.DataObject, o.Code)] = DataObjectSource(o.Code, o.NameAr, o.NameEn, o.DbSchema, o.DbObject, o.Columns);
        }

        foreach (var c in await context.Classes.AsNoTracking().Include(c => c.Attributes).Where(c => c.IsActive).ToListAsync(cancellationToken))
        {
            var columns = ClassColumns.Select(f => new LovSourceColumn(f.Name, f.Type, LovQueryCompiler.CategoryOf(f.Type), f.Ar, f.En)).ToList();
            columns.AddRange(c.Attributes.Where(a => a.IsReportable).OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code).Select(a =>
            {
                var type = a.IsMultiple ? "JSONB" : a.DataType switch
                {
                    AttributeDataType.Number => "NUMERIC",
                    AttributeDataType.Date => "DATE",
                    AttributeDataType.Bool => "BOOLEAN",
                    AttributeDataType.Json => "JSONB",
                    _ => "TEXT",
                };
                return new LovSourceColumn(ClassViewGenerator.SqlName(a.Code), type, LovQueryCompiler.CategoryOf(type), a.NameAr, a.NameEn);
            }));
            sources[(LovQueryKinds.Class, c.Code)] = new LovQuerySourceInfo(LovQueryKinds.Class, c.Code, c.NameAr, c.NameEn, "apps", ClassViewGenerator.ViewName(c.Code), columns);
        }

        return _sources = sources;
    }
}
