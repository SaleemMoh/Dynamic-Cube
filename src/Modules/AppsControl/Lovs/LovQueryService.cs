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
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>What a preview shows: the SQL the definition becomes, the first values, and what is wrong with it.</summary>
/// <summary>A report's run: its columns, a page of rows, the number of rows (-1 when not asked), the totals row and the tiles.</summary>
public sealed record LovReportOutput(
    IReadOnlyList<(string Name, LovColumnCategory Category, string? Aggregate)> Columns,
    List<System.Text.Json.Nodes.JsonObject> Rows,
    long Total,
    System.Text.Json.Nodes.JsonObject? Totals,
    System.Text.Json.Nodes.JsonObject? Tiles);

public sealed record LovQueryPreview(string? Sql, IReadOnlyList<LovItem> Items, bool HasMore, IReadOnlyList<LovQueryProblem> Problems, string? Error);

/// <summary>
/// Sources of the query builder (registered data objects and classes, with the columns they offer) and the runs of
/// query LOVs. Queries run on a read-only transaction with a statement timeout; the data object's data scopes apply.
/// </summary>
public sealed class LovQueryService(AppsControlDbContext context, DataScopeService scopes, IOptions<AppsControlOptions> options)
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
    private Dictionary<(string Kind, string Code), LovQuerySourceInfo>? _withTables;

    /// <summary>
    /// Every source the builder may use: active data objects (their registered columns), classes, and the other tables
    /// and views of the schemas open to data objects (all their columns), which the list registers when it is saved.
    /// </summary>
    public async Task<IReadOnlyList<LovQuerySourceInfo>> SourcesAsync(CancellationToken cancellationToken) =>
        [.. (await LoadWithTablesAsync(cancellationToken)).Values.OrderBy(s => s.Kind, StringComparer.Ordinal).ThenBy(s => s.Code, StringComparer.Ordinal)];

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
        var (compiled, problems) = await CompileAsync(query, access, new LovQueryRun(Facts(access), null, search, null, limit + 1, 0), null, cancellationToken, withTables: true);
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

    /// <param name="withTables">While building (the preview): tables not registered yet may be read too.</param>
    private async Task<(LovCompiledQuery? Query, IReadOnlyList<LovQueryProblem> Problems)> CompileAsync(
        LovQueryDefinition? query, AccessSnapshot access, LovQueryRun run, string? resource, CancellationToken cancellationToken, bool withTables = false)
    {
        var sources = withTables ? await LoadWithTablesAsync(cancellationToken) : await LoadAsync(cancellationToken);
        var scoped = await ScopesAsync(query, access, resource, cancellationToken);
        return LovQueryCompiler.Compile(query, (kind, code) => sources.GetValueOrDefault((kind, code)), run with { Scopes = scoped });
    }

    /// <summary>Data scopes of the data objects a query reads (the same rule filters lists, inboxes and reports).</summary>
    private async Task<Dictionary<string, IReadOnlyList<DataScopeFilter>>> ScopesAsync(LovQueryDefinition? query, AccessSnapshot access, string? resource, CancellationToken cancellationToken)
    {
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

        return scoped;
    }

    /// <summary>Problems of a report's query over the registered sources (ADR-0045).</summary>
    public async Task<IReadOnlyList<LovQueryProblem>> CheckReportAsync(LovQueryDefinition? query, LovReportSpec spec, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken)
    {
        var sources = await LoadAsync(cancellationToken);
        return LovQueryCompiler.CheckReport(query, (kind, code) => kind == LovQueryKinds.Table ? null : sources.GetValueOrDefault((kind, code)), spec, inputs);
    }

    /// <summary>The columns of a report's result, or null when the query has problems.</summary>
    public async Task<IReadOnlyList<(string Name, LovColumnCategory Category, string? Aggregate)>?> DescribeReportAsync(
        LovQueryDefinition? query, LovReportSpec spec, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken)
    {
        var sources = await LoadAsync(cancellationToken);
        return LovQueryCompiler.DescribeReport(query, (kind, code) => kind == LovQueryKinds.Table ? null : sources.GetValueOrDefault((kind, code)), spec, inputs);
    }

    /// <summary>
    /// One page of a report for a user: their facts answer conditions on the user's data, their data scopes (for the report's
    /// resource) filter the data objects read; with the summary, the number of rows, the totals row and the tiles too.
    /// </summary>
    public async Task<(LovReportOutput? Output, IReadOnlyList<LovQueryProblem> Problems)> ReportAsync(
        LovQueryDefinition? query,
        LovReportSpec spec,
        AccessSnapshot access,
        string? resource,
        IReadOnlyDictionary<string, string?> inputs,
        int limit,
        int offset,
        bool withSummary,
        CancellationToken cancellationToken)
    {
        var sources = await LoadAsync(cancellationToken);
        var scoped = await ScopesAsync(query, access, resource, cancellationToken);
        var run = new LovQueryRun(Facts(access), Limit: limit, Offset: offset, Scopes: scoped, Inputs: inputs);
        var (compiled, problems) = LovQueryCompiler.CompileReport(query, (kind, code) => kind == LovQueryKinds.Table ? null : sources.GetValueOrDefault((kind, code)), spec, run);
        if (compiled is null)
        {
            return (null, problems);
        }

        var names = compiled.Rows.Fields!;
        var rows = await ExecuteAsync(compiled.Rows, reader => DataRowReader.Read(reader, names), cancellationToken);
        long total = -1;
        System.Text.Json.Nodes.JsonObject? totals = null;
        System.Text.Json.Nodes.JsonObject? tiles = null;
        if (withSummary)
        {
            total = (await ExecuteAsync(compiled.Count, reader => Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture), cancellationToken)).FirstOrDefault();
            if (compiled.Totals is not null)
            {
                totals = (await ExecuteAsync(compiled.Totals, reader => DataRowReader.Read(reader, names), cancellationToken)).FirstOrDefault();
            }

            if (compiled.Tiles is not null)
            {
                var tileNames = compiled.Tiles.Fields!;
                tiles = (await ExecuteAsync(compiled.Tiles, reader => DataRowReader.Read(reader, tileNames), cancellationToken)).FirstOrDefault();
            }
        }

        return (new LovReportOutput(compiled.Columns, rows, total, totals, tiles), []);
    }

    /// <summary>Problems of a public endpoint's query over the registered sources (ADR-0042).</summary>
    public async Task<IReadOnlyList<LovQueryProblem>> CheckRowsAsync(LovQueryDefinition? query, IReadOnlyList<LovQueryField> fields, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken)
    {
        var sources = await LoadAsync(cancellationToken);
        return LovQueryCompiler.CheckRows(query, (kind, code) => kind == LovQueryKinds.Table ? null : sources.GetValueOrDefault((kind, code)), fields, inputs);
    }

    /// <summary>The kind of value each field of a public endpoint's query returns, or null when the query has problems.</summary>
    public async Task<IReadOnlyList<(string Name, LovColumnCategory Category)>?> DescribeRowsAsync(
        LovQueryDefinition? query, IReadOnlyList<LovQueryField> fields, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken)
    {
        var sources = await LoadAsync(cancellationToken);
        return LovQueryCompiler.DescribeRows(query, (kind, code) => kind == LovQueryKinds.Table ? null : sources.GetValueOrDefault((kind, code)), fields, inputs);
    }

    /// <summary>
    /// One page of a public endpoint's rows for the account a call runs as (ADR-0042): its facts answer the conditions on the
    /// user's data, its inputs the conditions on inputs, its data scopes (for the endpoint's resource) filter the data
    /// objects read, and a table's rows are those of its company and branches. Without an account (the designer's try as
    /// administrator) nothing is filtered.
    /// </summary>
    public async Task<(List<System.Text.Json.Nodes.JsonObject>? Rows, IReadOnlyList<LovQueryProblem> Problems)> RowsAsync(
        LovQueryDefinition? query, IReadOnlyList<LovQueryField> fields, LovQueryRun run, AccessSnapshot? access, string? resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var sources = await LoadAsync(cancellationToken);
        if (access is not null)
        {
            run = run with { Scopes = await ScopesAsync(query, access, resource, cancellationToken) };
        }

        var (compiled, problems) = LovQueryCompiler.CompileRows(query, (kind, code) => kind == LovQueryKinds.Table ? null : sources.GetValueOrDefault((kind, code)), fields, run);
        if (compiled is null)
        {
            return (null, problems);
        }

        var names = compiled.Fields!;
        return (await ExecuteAsync(compiled, reader => DataRowReader.Read(reader, names), cancellationToken), []);
    }

    private Task<List<LovItem>> RunAsync(LovCompiledQuery compiled, CancellationToken cancellationToken) =>
        ExecuteAsync(compiled, reader => new LovItem(
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3)), cancellationToken);

    /// <summary>Runs compiled SQL in a read-only transaction with the statement timeout, reading each row.</summary>
    private async Task<List<T>> ExecuteAsync<T>(LovCompiledQuery compiled, Func<DbDataReader, T> read, CancellationToken cancellationToken)
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

            var items = new List<T>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(read(reader));
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

    /// <summary>The registered sources and, besides, the tables and views of the open schemas that no data object registers.</summary>
    private async Task<Dictionary<(string Kind, string Code), LovQuerySourceInfo>> LoadWithTablesAsync(CancellationToken cancellationToken)
    {
        if (_withTables is not null)
        {
            return _withTables;
        }

        var sources = new Dictionary<(string Kind, string Code), LovQuerySourceInfo>(await LoadAsync(cancellationToken));
        var registered = sources.Values.Where(s => s.Kind == LovQueryKinds.DataObject).Select(s => (s.Schema, s.DbObject)).ToHashSet();
        foreach (var table in await DatabaseCatalog.GetTablesAsync(context, options.Value.DataObjectSchemas, cancellationToken))
        {
            if (registered.Contains((table.Schema, table.Name)))
            {
                continue;
            }

            var code = $"{table.Schema}.{table.Name}";
            var columns = table.Columns.Select(c => new LovSourceColumn(c.Column, c.DataType, LovQueryCompiler.CategoryOf(c.DataType), null, null)).ToList();
            sources[(LovQueryKinds.Table, code)] = new LovQuerySourceInfo(LovQueryKinds.Table, code, code, code, table.Schema, table.Name, columns, table.IsView ? "VIEW" : "TABLE");
        }

        return _withTables = sources;
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
