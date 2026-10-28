using Microsoft.EntityFrameworkCore;

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

        return builder
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", historySchema);
                if (migrationsAssembly is not null)
                {
                    npgsql.MigrationsAssembly(migrationsAssembly);
                }
            })
            .UseSnakeCaseNamingConvention();
    }
}
