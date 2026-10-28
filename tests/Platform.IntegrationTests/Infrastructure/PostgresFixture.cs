using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

[assembly: AssemblyFixture(typeof(DynamicCube.Platform.IntegrationTests.Infrastructure.PostgresFixture))]

namespace DynamicCube.Platform.IntegrationTests.Infrastructure;

/// <summary>
/// Creates a throw-away database for the test run on the server given by <c>DYNAMICCUBE_TEST_POSTGRES</c>
/// (a connection string with rights to create databases), applies the platform migrations and creates the test
/// module's tables. Tests are skipped when the variable is not set.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string EnvironmentVariable = "DYNAMICCUBE_TEST_POSTGRES";

    private string? _adminConnectionString;
    private string? _databaseName;

    public string? ConnectionString { get; private set; }

    public async ValueTask InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            return;
        }

        _databaseName = "dc_test_" + Guid.NewGuid().ToString("N")[..12];
        await ExecuteAdminAsync($"CREATE DATABASE \"{_databaseName}\"");
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }.ConnectionString;

        await using (var platform = CreatePlatformContext())
        {
            await platform.Database.MigrateAsync();
        }

        await using (var module = CreateTestModuleContext())
        {
            await module.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_databaseName is not null)
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
        }
    }

    public string RequireConnectionString()
    {
        if (ConnectionString is null)
        {
            Assert.Skip($"Set {EnvironmentVariable} to run PostgreSQL integration tests.");
        }

        return ConnectionString;
    }

    private PlatformDbContext CreatePlatformContext()
    {
        var builder = new DbContextOptionsBuilder<PlatformDbContext>();
        builder.UseDynamicCubePostgres(ConnectionString!, PlatformSchemas.Platform);
        return new PlatformDbContext(builder.Options);
    }

    private TestModuleDbContext CreateTestModuleContext()
    {
        var builder = new DbContextOptionsBuilder<TestModuleDbContext>();
        builder.UseDynamicCubePostgres(ConnectionString!, TestModuleDbContext.Schema);
        return new TestModuleDbContext(builder.Options);
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
