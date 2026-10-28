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

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
