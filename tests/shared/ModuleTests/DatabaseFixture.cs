using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Seeding;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Modules.Parties.Seeding;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DynamicCube.Tests.Infrastructure;

/// <summary>
/// Throw-away database on the server given by <c>DYNAMICCUBE_TEST_POSTGRES</c> with the migrations and system rows of
/// the platform and the Apps Control, Integration and Party modules. Database tests are skipped when the variable is not set.
/// Shared (as linked source) by the module test projects; each registers it with <c>[assembly: AssemblyFixture]</c>.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
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

        _databaseName = "dc_test_modules_" + Guid.NewGuid().ToString("N")[..10];
        await ExecuteAdminAsync($"CREATE DATABASE \"{_databaseName}\"");
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }.ConnectionString;

        await using var host = new TestHost(ConnectionString);
        await host.AsAsync("migrator", async sp =>
        {
            await sp.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<AppsControlDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<IntegrationDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<PartiesDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<AppsControlSeeder>().SeedAsync(CancellationToken.None);
            await sp.GetRequiredService<PartiesSeeder>().SeedAsync(CancellationToken.None);
        });
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

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
