using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Reports;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>
/// Report queries (ADR-0045) on the visual query builder's compiler: a report reads registered tables and classes for a
/// viewer — the signed-in user, or a stored user a scheduled report is sent to — with their facts, company, branches and
/// data scopes, read-only and within the time limit.
/// </summary>
internal sealed class ReportQueries(LovQueryService queries, AccessResolver access) : IReportQueries
{
    /// <summary>The app reports run in: the internal system, whose roles and data scopes apply.</summary>
    private const string App = "INTERNAL";

    public async Task<IReadOnlyDictionary<string, string[]>> CheckAsync(JsonNode? query, ReportShape shape, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var definition = Parse(query, out var error);
        if (error is not null)
        {
            return new Dictionary<string, string[]> { ["query"] = [error] };
        }

        var problems = await queries.CheckReportAsync(definition, Spec(shape), inputs, cancellationToken);
        return problems.GroupBy(p => p.Field, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(p => p.Message).ToArray(), StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<ReportResultColumn>> DescribeAsync(JsonNode? query, ReportShape shape, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var definition = Parse(query, out _);
        var columns = definition is null ? null : await queries.DescribeReportAsync(definition, Spec(shape), inputs, cancellationToken);
        return [.. (columns ?? []).Select(c => new ReportResultColumn(c.Name, TypeOf(c.Category), c.Aggregate))];
    }

    public async Task<ReportResult> RunAsync(
        JsonNode? query,
        ReportShape shape,
        IReadOnlyDictionary<string, string?> inputs,
        ReportViewer viewer,
        string? resource,
        int limit,
        int offset,
        bool withSummary,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var definition = Parse(query, out var error) ?? throw new InvalidOperationException(error ?? "The report has no query.");
        var snapshot = await SnapshotAsync(viewer, cancellationToken);
        var (output, problems) = await queries.ReportAsync(definition, Spec(shape), snapshot, resource, inputs, limit + 1, offset, withSummary, cancellationToken);
        if (output is null)
        {
            // Problems at a filter are the viewer's to fix; any other means the report no longer fits the tables.
            var bad = problems.Where(p => p.Field.StartsWith("inputs.", StringComparison.Ordinal)).ToList();
            if (bad.Count > 0)
            {
                throw new ReportInputException(bad.GroupBy(p => p.Field["inputs.".Length..], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(p => p.Message).ToArray(), StringComparer.Ordinal));
            }

            throw new InvalidOperationException("The report's query no longer runs: " + string.Join("; ", problems.Select(p => $"{p.Field}: {p.Message}")));
        }

        return new ReportResult(
            [.. output.Columns.Select(c => new ReportResultColumn(c.Name, TypeOf(c.Category), c.Aggregate))],
            output.Rows.Take(limit).ToList(),
            output.Rows.Count > limit,
            output.Total,
            output.Totals,
            output.Tiles);
    }

    public async Task<bool> CanAsync(ReportViewer viewer, string resource, string action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        try
        {
            return (await SnapshotAsync(viewer, cancellationToken)).Can(resource, action);
        }
        catch (ReportAccessException)
        {
            return false;
        }
    }

    private async Task<AccessSnapshot> SnapshotAsync(ReportViewer viewer, CancellationToken cancellationToken) =>
        viewer.UserName is null
            ? viewer.Company is null ? await access.GetAsync(cancellationToken) : await access.GetInCompanyAsync(viewer.Company, cancellationToken)
            : await access.GetForUserAsync(viewer.UserName, App, viewer.Company, cancellationToken)
              ?? throw new ReportAccessException($"User '{viewer.UserName}' does not exist or is not active.");

    private static LovReportSpec Spec(ReportShape shape) => new(
        [.. shape.Columns.Select(c => new LovReportColumn(c.Name, c.Column, c.Aggregate))],
        [.. shape.Tiles.Select(t => new LovReportTile(t.Name, t.Column, t.Aggregate))],
        [.. shape.Sort.Select(s => (s.Column, s.Descending))],
        shape.Totals);

    private static string TypeOf(LovColumnCategory category) => category switch
    {
        LovColumnCategory.Number => DataTypes.Number,
        LovColumnCategory.Date => DataTypes.Date,
        LovColumnCategory.Bool => DataTypes.Boolean,
        LovColumnCategory.Json => DataTypes.Json,
        _ => DataTypes.Text,
    };

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
