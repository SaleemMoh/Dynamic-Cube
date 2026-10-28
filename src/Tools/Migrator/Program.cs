using DynamicCube.Hosting;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Applies database migrations for the kernel and every module, in dependency order.
// Run as a Kubernetes Job / init step, or manually during N-tier upgrades. Exit code 0 = success.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.AddDynamicCubeDefaults("MIGRATOR");
builder.Services.AddSingleton<ICurrentUser>(new SystemUser("MIGRATOR"));

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");

try
{
    await MigrateAsync<PlatformDbContext>(host.Services, logger);
    // Module contexts are appended here as modules are added (Apps Control first).
    logger.LogInformation("All migrations applied");
    return 0;
}
#pragma warning disable CA1031 // Report any failure through the exit code.
catch (Exception ex)
#pragma warning restore CA1031
{
    logger.LogCritical(ex, "Migration failed");
    return 1;
}

static async Task MigrateAsync<TContext>(IServiceProvider services, ILogger logger)
    where TContext : ModuleDbContext
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TContext>();
    var pending = (await context.Database.GetPendingMigrationsAsync()).Count();
    MigratorLog.PendingMigrations(logger, typeof(TContext).Name, pending);
    await context.Database.MigrateAsync();
}

internal static partial class MigratorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Context}: {Count} pending migration(s)")]
    public static partial void PendingMigrations(ILogger logger, string context, int count);
}
