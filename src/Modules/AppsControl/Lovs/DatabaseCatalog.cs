using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DynamicCube.Modules.AppsControl.Lovs;

internal static class DatabaseCatalog
{
    /// <summary>Columns (name → upper-case data type) of a table or view, empty when it does not exist.</summary>
    public static async Task<Dictionary<string, string>> GetColumnsAsync(DbContext context, string schema, string objectName, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = @schema AND table_name = @name ORDER BY ordinal_position";
            AddParameter(command, "schema", schema);
            AddParameter(command, "name", objectName);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result[reader.GetString(0)] = reader.GetString(1).ToUpperInvariant();
            }

            return result;
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Tables and views of the given schemas with their columns (upper-case data types), in order; migrations history
    /// tables and the generated class views (offered as classes) are left out.
    /// </summary>
    public static async Task<List<(string Schema, string Name, bool IsView, List<(string Column, string DataType)> Columns)>> GetTablesAsync(
        DbContext context, IReadOnlyCollection<string> schemas, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT c.table_schema, c.table_name, t.table_type, c.column_name, c.data_type
                FROM information_schema.columns c
                JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name
                WHERE c.table_schema = ANY(@schemas)
                  AND c.table_name NOT LIKE '\_\_%'
                  AND NOT (c.table_schema = 'apps' AND c.table_name LIKE 'v\_class\_%')
                ORDER BY c.table_schema, c.table_name, c.ordinal_position
                """;
            AddParameter(command, "schemas", schemas.ToArray());
            var result = new List<(string, string, bool, List<(string, string)>)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var (schema, name) = (reader.GetString(0), reader.GetString(1));
                if (result.Count == 0 || result[^1].Item1 != schema || result[^1].Item2 != name)
                {
                    result.Add((schema, name, reader.GetString(2) == "VIEW", []));
                }

                result[^1].Item4.Add((reader.GetString(3), reader.GetString(4).ToUpperInvariant()));
            }

            return result;
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
