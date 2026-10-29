using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Hosting;

/// <summary>
/// Tells when the database is behind the code (the migrator has not been run after an update): every request touching a
/// new table would otherwise fail with a bare server error. Logged at startup and reported by the readiness check.
/// </summary>
public static partial class MigrationCheck
{
    public const string HowToFix = "Run the migrator (dotnet run --project src/Tools/Migrator) and restart.";

    public static IServiceCollection AddMigrationCheck(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<PendingMigrationsHealthCheck>("migrations", tags: [HostDefaults.ReadyTag]);
        services.AddHostedService<PendingMigrationsStartupLog>();
        return services;
    }

    /// <summary>Migrations of the registered module contexts that are not applied yet, as "Context: migration".</summary>
    public static async Task<List<string>> PendingAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var pending = new List<string>();
        foreach (var registration in scope.ServiceProvider.GetServices<ModuleContextRegistration>().DistinctBy(r => r.ContextType))
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            var names = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            pending.AddRange(names.Select(name => $"{registration.ContextType.Name}: {name}"));
        }

        return pending;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The database is not up to date; {Count} migration(s) are not applied: {Pending}. {HowToFix}")]
    private static partial void NotUpToDate(ILogger logger, int count, string pending, string howToFix);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check the database migrations at startup.")]
    private static partial void CheckFailed(ILogger logger, Exception exception);

    private sealed class PendingMigrationsHealthCheck(IServiceProvider services) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var pending = await PendingAsync(services, cancellationToken);
            return pending.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Migrations not applied: {string.Join(", ", pending)}. {HowToFix}");
        }
    }

    private sealed class PendingMigrationsStartupLog(IServiceProvider services, ILogger<PendingMigrationsStartupLog> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var pending = await PendingAsync(services, stoppingToken);
                if (pending.Count > 0)
                {
                    NotUpToDate(logger, pending.Count, string.Join(", ", pending), HowToFix);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                CheckFailed(logger, exception);
            }
        }
    }
}
