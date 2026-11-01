using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.PublicApi;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>Reads a row of chosen columns as JSON: numbers as numbers, yes/no as true/false, dates as YYYY-MM-DD, JSON as JSON.</summary>
internal static class DataRowReader
{
    public static JsonObject Read(DbDataReader reader, IReadOnlyList<(string Name, LovColumnCategory Category)> fields)
    {
        var row = new JsonObject();
        for (var i = 0; i < fields.Count; i++)
        {
            row[fields[i].Name] = reader.IsDBNull(i) ? null : Value(reader.GetValue(i), fields[i].Category);
        }

        return row;
    }

    private static JsonNode? Value(object value, LovColumnCategory category) => value switch
    {
        string text when category == LovColumnCategory.Json => Parse(text),
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        DateTime date => JsonValue.Create(date.TimeOfDay == TimeSpan.Zero ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : date.ToString("O", CultureInfo.InvariantCulture)),
        DateTimeOffset at => JsonValue.Create(at.ToString("O", CultureInfo.InvariantCulture)),
        DateOnly day => JsonValue.Create(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        int or long or short or decimal or double or float => JsonValue.Create(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
        Guid id => JsonValue.Create(id.ToString()),
        string[] list => new JsonArray([.. list.Select(x => (JsonNode?)JsonValue.Create(x))]),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
    }
}

/// <summary>Public endpoints' read-data queries (ADR-0042), on the visual query builder's compiler and its read-only runs.</summary>
internal sealed class DataQueries(LovQueryService queries, ApiAccess access) : IDataQueries
{
    public async Task<IReadOnlyDictionary<string, string[]>> CheckAsync(JsonNode? query, IReadOnlyList<DataField> fields, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken)
    {
        var definition = Parse(query, out var parseError);
        if (parseError is not null)
        {
            return new Dictionary<string, string[]> { ["query"] = [parseError] };
        }

        var problems = await queries.CheckRowsAsync(definition, Fields(fields), inputs, cancellationToken);
        return problems.GroupBy(p => p.Field, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(p => p.Message).ToArray(), StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, string>> TypesAsync(JsonNode? query, IReadOnlyList<DataField> fields, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken)
    {
        var definition = Parse(query, out _);
        var described = definition is null ? null : await queries.DescribeRowsAsync(definition, Fields(fields), inputs, cancellationToken);
        return (described ?? []).ToDictionary(
            f => f.Name,
            f => f.Category switch
            {
                LovColumnCategory.Number => DataTypes.Number,
                LovColumnCategory.Date => DataTypes.Date,
                LovColumnCategory.Bool => DataTypes.Boolean,
                LovColumnCategory.Json => DataTypes.Json,
                _ => DataTypes.Text,
            },
            StringComparer.Ordinal);
    }

    public async Task<DataRows> RunAsync(
        JsonNode? query,
        IReadOnlyList<DataField> fields,
        IReadOnlyDictionary<string, string?> inputs,
        ApiCaller caller,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var definition = Parse(query, out var parseError) ?? throw new InvalidOperationException(parseError ?? "The endpoint has no query.");

        // The account the call runs as (the client's service account, or the person it acts for): its facts, company and
        // branches, and its data scopes on the endpoint's resource. Without an account only the caller's company and branches
        // apply (none at all for the designer's try as administrator).
        var snapshot = await access.SnapshotAsync(caller, cancellationToken);
        var facts = snapshot is null
            ? new LovUserFacts(caller.Company, caller.Branches, null, [], [], "api:" + caller.ClientCode, "API", [], AllCompanies: caller.Company is null)
            : LovQueryService.Facts(snapshot) with { Branches = caller.Branches };
        var (rows, problems) = await queries.RowsAsync(definition, Fields(fields), new LovQueryRun(facts, Limit: limit + 1, Offset: offset, Inputs: inputs), snapshot, caller.Resource, cancellationToken);
        if (rows is null)
        {
            // Problems at an input are the caller's (400); any other means the endpoint no longer fits the tables.
            var bad = problems.Where(p => p.Field.StartsWith("inputs.", StringComparison.Ordinal)).ToList();
            if (bad.Count > 0)
            {
                throw new ApiInputException(bad.GroupBy(p => p.Field["inputs.".Length..], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(p => p.Message).ToArray(), StringComparer.Ordinal));
            }

            throw new InvalidOperationException("The endpoint's query no longer runs: " + string.Join("; ", problems.Select(p => $"{p.Field}: {p.Message}")));
        }

        return new DataRows(rows.Take(limit).ToList(), rows.Count > limit);
    }

    private static List<LovQueryField> Fields(IReadOnlyList<DataField> fields) => [.. fields.Select(f => new LovQueryField(f.Name, f.Column))];

    private static LovQueryDefinition? Parse(JsonNode? query, out string? error)
    {
        error = null;
        if (query is null)
        {
            error = "Build the query: choose where the data comes from.";
            return null;
        }

        try
        {
            return query.Deserialize<LovQueryDefinition>(ChangeJson.Options);
        }
        catch (JsonException ex)
        {
            error = "The query cannot be read: " + ex.Message;
            return null;
        }
    }
}
