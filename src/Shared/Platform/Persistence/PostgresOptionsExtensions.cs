using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DynamicCube.Platform.Persistence;

public static class PostgresOptionsExtensions
{
    public const string ConnectionStringName = "DynamicCube";

    /// <summary>
    /// Standard PostgreSQL configuration for a module context: snake_case names and a migrations history table
    /// inside the module's own schema.
    /// </summary>
    public static DbContextOptionsBuilder UseDynamicCubePostgres(
        this DbContextOptionsBuilder builder,
        string connectionString,
        string historySchema,
        string? migrationsAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseNpgsql(connectionString, npgsql => Configure(npgsql, historySchema, migrationsAssembly)).UseSnakeCaseNamingConvention();
    }

    /// <summary>The same configuration on a connection shared with other module contexts (see <see cref="ModuleConnection"/>).</summary>
    public static DbContextOptionsBuilder UseDynamicCubePostgres(
        this DbContextOptionsBuilder builder,
        DbConnection connection,
        string historySchema,
        string? migrationsAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connection);

        return builder.UseNpgsql(connection, npgsql => Configure(npgsql, historySchema, migrationsAssembly)).UseSnakeCaseNamingConvention();
    }

    private static void Configure(NpgsqlDbContextOptionsBuilder npgsql, string historySchema, string? migrationsAssembly)
    {
        npgsql.MigrationsHistoryTable("__ef_migrations_history", historySchema);
        if (migrationsAssembly is not null)
        {
            npgsql.MigrationsAssembly(migrationsAssembly);
        }
    }
}
