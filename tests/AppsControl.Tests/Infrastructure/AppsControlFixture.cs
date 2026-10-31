using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Seeding;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

[assembly: AssemblyFixture(typeof(DynamicCube.Modules.AppsControl.Tests.Infrastructure.AppsControlFixture))]

namespace DynamicCube.Modules.AppsControl.Tests.Infrastructure;

/// <summary>
/// Creates a throw-away database on the server given by <c>DYNAMICCUBE_TEST_POSTGRES</c>, applies the platform and
/// Apps Control migrations and seeds the SYSTEM rows. Tests are skipped when the variable is not set.
/// </summary>
public sealed class AppsControlFixture : IAsyncLifetime
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

        _databaseName = "dc_test_apps_" + Guid.NewGuid().ToString("N")[..10];
        await ExecuteAdminAsync($"CREATE DATABASE \"{_databaseName}\"");
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }.ConnectionString;

        await using var services = TestServices.Build(ConnectionString, new TestUser("migrator"));
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AppsControlDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AppsControlSeeder>().SeedAsync(CancellationToken.None);
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

/// <summary>Switchable signed-in user (subject <c>sub-&lt;username&gt;</c>).</summary>
public sealed class TestUser(string userName) : ICurrentUser
{
    public string Name { get; set; } = userName;

    public string UserId => "sub-" + Name;

    public string? UserName => Name;

    public bool IsAuthenticated => true;

    public string AppCode => "INTERNAL";

    /// <summary>The company sent as the workspace header (ADR-0036 step 5); null sends none.</summary>
    public string? Company { get; set; }

    public string? RequestedCompany => Company;
}
