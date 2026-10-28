using DynamicCube.Platform.IntegrationTests.Infrastructure;
using Npgsql;
using Shouldly;

namespace DynamicCube.Platform.IntegrationTests;

public sealed class MigrationTests(PostgresFixture database)
{
    [Fact]
    public async Task Platform_migration_creates_kernel_tables_in_their_schemas()
    {
        var connectionString = database.RequireConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT table_schema || '.' || table_name FROM information_schema.tables
             WHERE table_schema IN ('platform', 'audit', 'cfg') ORDER BY 1
            """,
            connection);

        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        tables.ShouldBe(
        [
            "audit.audit_log",
            "cfg.change_items",
            "cfg.change_sets",
            "platform.__ef_migrations_history",
            "platform.data_protection_keys",
            "platform.outbox_messages",
        ]);
    }
}
