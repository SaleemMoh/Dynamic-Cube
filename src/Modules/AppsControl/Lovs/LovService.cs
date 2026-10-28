using System.Data;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Lovs;

public sealed class LovNotFoundException(string code) : Exception($"LOV '{code}' was not found.")
{
    public LovNotFoundException()
        : this(string.Empty)
    {
    }

    public LovNotFoundException(string message, Exception innerException)
        : this(message) => _ = innerException;
}

/// <summary>
/// Reads LOV values: generates the query for the LOV's source and always applies the user's grant, so the same rule
/// governs every dropdown that uses the LOV.
/// </summary>
public sealed class LovService(AppsControlDbContext context, GrantResolver grants, SqlLovRunner sqlRunner, JsonLogicEvaluator evaluator, TimeProvider timeProvider)
{
    public const int DefaultPageSize = 50;

    /// <param name="publicOnly">True on the external host: only LOVs marked public are served.</param>
    public async Task<LovPage> GetValuesAsync(string lovCode, AccessSnapshot access, LovQuery query, bool publicOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(query);

        var lov = await context.Lovs.AsNoTracking()
            .Include(l => l.Class)
            .Include(l => l.DataObject)
            .SingleOrDefaultAsync(l => l.Code == lovCode && l.IsActive, cancellationToken);
        if (lov is null || (publicOnly && !lov.IsPublic))
        {
            throw new LovNotFoundException(lovCode);
        }

        var grant = await grants.ResolveAsync(lov, access, query.Resource, cancellationToken);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? (lov.SearchMode == LovSearchMode.Full ? lov.MaxRows : DefaultPageSize) : query.PageSize, 1, lov.MaxRows);
        var page = Math.Max(1, query.Page);
        var offset = (page - 1) * pageSize;
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        if (!grant.All && grant.Values.Count == 0)
        {
            return new LovPage(lov.Code, [], null, false);
        }

        // One extra row tells whether another page exists.
        var rows = lov.SourceType switch
        {
            LovSourceType.Class => await ClassValuesAsync(lov, access, query.Parent, search, grant, pageSize + 1, offset, cancellationToken),
            LovSourceType.DataObject => await DataObjectValuesAsync(lov, query.Parent, search, grant, pageSize + 1, offset, cancellationToken),
            LovSourceType.Sql => await sqlRunner.QueryAsync(lov.SqlText!, SqlLovColumns.From(lov.ValueColumn, lov.DisplayArColumn, lov.DisplayEnColumn, lov.ParentColumn), access.User, query.Parent, search, grant, pageSize + 1, offset, cancellationToken),
            _ => [],
        };

        var hasMore = rows.Count > pageSize;
        var items = hasMore ? rows.Take(pageSize).ToList() : rows;
        var defaultValue = grant.DefaultValue;
        if (defaultValue is null && lov.SourceType == LovSourceType.Class)
        {
            defaultValue = await context.Domains.AsNoTracking()
                .Where(d => d.ClassId == lov.ClassId && d.IsDefault && d.IsActive)
                .Select(d => d.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (defaultValue is not null && !grant.Allows(defaultValue))
        {
            defaultValue = null;
        }

        return new LovPage(lov.Code, items, defaultValue, hasMore);
    }

    private async Task<List<LovItem>> ClassValuesAsync(Lov lov, AccessSnapshot access, string? parent, string? search, GrantSet grant, int limit, int offset, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var domains = context.Domains.AsNoTracking()
            .Where(d => d.ClassId == lov.ClassId && d.IsActive
                && (d.EffectiveFrom == null || d.EffectiveFrom <= today) && (d.EffectiveTo == null || d.EffectiveTo >= today));
        if (parent is not null)
        {
            domains = domains.Where(d => d.ParentDomain!.Code == parent);
        }

        if (!grant.All)
        {
            var values = grant.Values.ToList();
            domains = domains.Where(d => values.Contains(d.Code));
        }

        if (search is not null)
        {
            var pattern = $"%{SqlLovRunner.LikeEscape(search)}%";
            domains = domains.Where(d => EF.Functions.ILike(d.NameAr, pattern) || EF.Functions.ILike(d.NameEn, pattern) || EF.Functions.ILike(d.Code, pattern));
        }

        var ordered = domains.OrderBy(d => d.DisplayOrder).ThenBy(d => d.NameAr);
        if (lov.StaticFilter is null)
        {
            return await ordered
                .Skip(offset).Take(limit)
                .Select(d => new LovItem(d.Code, d.NameAr, d.NameEn, d.ParentDomain != null ? d.ParentDomain.Code : null))
                .ToListAsync(cancellationToken);
        }

        // Static filters are JSONLogic over each value (same evaluator as the browser), applied in memory.
        var rule = JsonNode.Parse(lov.StaticFilter);
        var userJson = access.User.ToJson();
        var candidates = await ordered
            .Select(d => new { d.Code, d.NameAr, d.NameEn, Parent = d.ParentDomain != null ? d.ParentDomain.Code : null, d.Attributes })
            .ToListAsync(cancellationToken);
        return candidates
            .Where(d => evaluator.EvaluateTruthy(rule, new JsonObject
            {
                ["code"] = d.Code,
                ["parent"] = d.Parent,
                ["attributes"] = JsonNode.Parse(d.Attributes),
                ["user"] = userJson["user"]!.DeepClone(),
                ["app"] = access.User.App,
            }))
            .Skip(offset).Take(limit)
            .Select(d => new LovItem(d.Code, d.NameAr, d.NameEn, d.Parent))
            .ToList();
    }

    private async Task<List<LovItem>> DataObjectValuesAsync(Lov lov, string? parent, string? search, GrantSet grant, int limit, int offset, CancellationToken cancellationToken)
    {
        var dataObject = lov.DataObject!;
        string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

        // Identifiers come from the registered data object (validated against the catalog when saved), never from the request.
        var value = Quote(lov.ValueColumn!);
        var displayAr = Quote(lov.DisplayArColumn!);
        var displayEn = Quote(lov.DisplayEnColumn!);
        var parentColumn = lov.ParentColumn is null ? "NULL" : $"{Quote(lov.ParentColumn)}::text";
        var sql = $"""
            SELECT {value}::text, {displayAr}::text, {displayEn}::text, {parentColumn}
            FROM {Quote(dataObject.DbSchema)}.{Quote(dataObject.DbObject)}
            WHERE (@p_parent::text IS NULL OR {parentColumn} = @p_parent)
              AND (@p_search::text IS NULL OR {displayAr}::text ILIKE @p_search OR {displayEn}::text ILIKE @p_search OR {value}::text ILIKE @p_search)
              AND (@p_all OR {value}::text = ANY(@p_values))
            ORDER BY 2
            LIMIT @p_limit OFFSET @p_offset
            """;

        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Identifiers are quoted catalog names; all values are bound parameters.
            command.CommandText = sql;
#pragma warning restore CA2100
            Add(command, "p_parent", parent);
            Add(command, "p_search", search is null ? null : $"%{SqlLovRunner.LikeEscape(search)}%");
            Add(command, "p_all", grant.All);
            Add(command, "p_values", grant.Values.ToArray());
            Add(command, "p_limit", limit);
            Add(command, "p_offset", offset);

            var items = new List<LovItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new LovItem(
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }

            return items;
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void Add(System.Data.Common.DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

/// <summary>Grant predicates for data objects: which values of which columns the current user may see.</summary>
public sealed class DataScopeService(AppsControlDbContext context, GrantResolver grants)
{
    public async Task<IReadOnlyList<DataScopeFilter>> GetFiltersAsync(string dataObjectCode, AccessSnapshot access, string? resourceCode, CancellationToken cancellationToken)
    {
        var bindings = await context.DataScopeBindings.AsNoTracking()
            .Include(b => b.Lov)
            .Where(b => context.DataObjects.Any(o => o.Id == b.DataObjectId && o.Code == dataObjectCode && o.IsActive))
            .ToListAsync(cancellationToken);

        var filters = new List<DataScopeFilter>();
        foreach (var binding in bindings.Where(b => b.AppliesTo.Count == 0 || (resourceCode is not null && b.AppliesTo.Contains(resourceCode))))
        {
            var grant = await grants.ResolveAsync(binding.Lov!, access, resourceCode, cancellationToken);
            if (!grant.All)
            {
                filters.Add(new DataScopeFilter(binding.ColumnName, binding.Lov!.Code, grant));
            }
        }

        return filters;
    }
}
