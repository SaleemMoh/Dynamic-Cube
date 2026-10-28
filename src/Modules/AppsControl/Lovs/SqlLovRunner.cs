using System.Data;
using DynamicCube.Modules.AppsControl.Access;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>
/// Runs restricted-SQL LOVs on a separate, read-only database role, inside a read-only transaction with a statement
/// timeout. The SQL must return <c>value</c>, <c>display_ar</c> and <c>display_en</c> columns and may use the bound
/// parameters <c>@user_id</c>, <c>@username</c>, <c>@org_unit</c> and <c>@app</c>.
/// </summary>
public sealed class SqlLovRunner : IDisposable
{
    private static readonly string[] RequiredColumns = ["value", "display_ar", "display_en"];

    private readonly NpgsqlDataSource? _dataSource;
    private readonly int _timeoutSeconds;

    public SqlLovRunner(IOptions<AppsControlOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timeoutSeconds = Math.Clamp(options.Value.LovSqlTimeoutSeconds, 1, 60);
        if (!string.IsNullOrWhiteSpace(options.Value.LovSqlConnectionString))
        {
            _dataSource = NpgsqlDataSource.Create(options.Value.LovSqlConnectionString);
        }
    }

    public bool IsEnabled => _dataSource is not null;

    /// <summary>Returns a problem description, or null when the SQL is usable.</summary>
    public async Task<string?> CheckAsync(string sql, string? parentColumn, CancellationToken cancellationToken)
    {
        if (sql.Contains(';', StringComparison.Ordinal))
        {
            return "Use a single SELECT statement without ';'.";
        }

        try
        {
            var columns = new List<string>();
            await RunAsync($"SELECT * FROM ({sql}) AS src LIMIT 0", UserContext.Sample(), _ => { }, async reader =>
            {
                var schema = await reader.GetColumnSchemaAsync(cancellationToken);
                columns.AddRange(schema.Select(c => c.ColumnName));
            }, cancellationToken);

            var missing = RequiredColumns.Concat(string.IsNullOrWhiteSpace(parentColumn) ? [] : [parentColumn]).Except(columns).ToList();
            return missing.Count == 0 ? null : $"The query must return the column(s): {string.Join(", ", missing)}.";
        }
        catch (PostgresException ex)
        {
            return ex.MessageText;
        }
    }

    public async Task<List<LovItem>> QueryAsync(
        string sql,
        string? parentColumn,
        UserContext user,
        string? parent,
        string? search,
        GrantSet grant,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var parentExpression = string.IsNullOrWhiteSpace(parentColumn) ? "NULL" : $"src.\"{parentColumn.Replace("\"", "\"\"", StringComparison.Ordinal)}\"::text";
        var text = $"""
            SELECT src.value::text, src.display_ar::text, src.display_en::text, {parentExpression}
            FROM ({sql}) AS src
            WHERE (@p_parent::text IS NULL OR {parentExpression} = @p_parent)
              AND (@p_search::text IS NULL OR src.display_ar::text ILIKE @p_search OR src.display_en::text ILIKE @p_search OR src.value::text ILIKE @p_search)
              AND (@p_all OR src.value::text = ANY(@p_values))
            ORDER BY 2
            LIMIT @p_limit OFFSET @p_offset
            """;
        var items = new List<LovItem>();
        await RunAsync(text, user, command =>
        {
            command.Parameters.AddWithValue("p_parent", (object?)parent ?? DBNull.Value);
            command.Parameters.AddWithValue("p_search", search is null ? DBNull.Value : $"%{LikeEscape(search)}%");
            command.Parameters.AddWithValue("p_all", grant.All);
            command.Parameters.AddWithValue("p_values", grant.Values.ToArray());
            command.Parameters.AddWithValue("p_limit", limit);
            command.Parameters.AddWithValue("p_offset", offset);
        }, async reader =>
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new LovItem(
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
        }, cancellationToken);
        return items;
    }

    public void Dispose() => _dataSource?.Dispose();

    internal static string LikeEscape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private async Task RunAsync(string sql, UserContext user, Action<NpgsqlCommand> bind, Func<NpgsqlDataReader, Task> read, CancellationToken cancellationToken)
    {
        if (_dataSource is null)
        {
            throw new InvalidOperationException("Restricted SQL is not enabled.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using (var setup = new NpgsqlCommand($"SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = {_timeoutSeconds * 1000}", connection, transaction))
        {
            await setup.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("user_id", (object?)user.UserId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("username", user.UserName);
        command.Parameters.AddWithValue("org_unit", (object?)user.PrimaryOrgUnit ?? DBNull.Value);
        command.Parameters.AddWithValue("app", user.App);
        bind(command);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await read(reader);
        }

        await transaction.RollbackAsync(cancellationToken);
    }
}
