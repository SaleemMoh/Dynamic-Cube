using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Tests.Infrastructure;

internal static class Sql
{
    /// <summary>Reads rows of a test query as column → value dictionaries.</summary>
    public static async Task<List<Dictionary<string, object?>>> QueryAsync(DbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Test-only SQL built from test constants.
            command.CommandText = sql;
#pragma warning restore CA2100
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(row);
            }

            return rows;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
