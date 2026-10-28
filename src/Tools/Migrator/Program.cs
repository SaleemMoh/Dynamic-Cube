using DynamicCube.Hosting;
using DynamicCube.Modules.AppsControl;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Seeding;
using DynamicCube.Modules.Communication;
using DynamicCube.Modules.Communication.Persistence;
using DynamicCube.Modules.Decisions;
using DynamicCube.Modules.Decisions.Persistence;
using DynamicCube.Modules.Documents;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Modules.Integration;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Modules.Parties;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Modules.Parties.Seeding;
using DynamicCube.Modules.Services;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Workflow;
using DynamicCube.Modules.Workflow.Persistence;
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
builder.Services.AddAppsControl(builder.Configuration, managesUsers: false);
builder.Services.AddIntegration(builder.Configuration);
builder.Services.AddParties(builder.Configuration);
builder.Services.AddServices(builder.Configuration);
builder.Services.AddWorkflow(builder.Configuration);
builder.Services.AddDocuments(builder.Configuration);
builder.Services.AddDecisions(builder.Configuration);
builder.Services.AddCommunication(builder.Configuration);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");

try
{
    await MigrateAsync<PlatformDbContext>(host.Services, logger);
    await MigrateAsync<AppsControlDbContext>(host.Services, logger);
    await MigrateAsync<IntegrationDbContext>(host.Services, logger);
    await MigrateAsync<PartiesDbContext>(host.Services, logger);
    await MigrateAsync<ServicesDbContext>(host.Services, logger);
    await MigrateAsync<WorkflowDbContext>(host.Services, logger);
    await MigrateAsync<DocumentsDbContext>(host.Services, logger);
    await MigrateAsync<DecisionsDbContext>(host.Services, logger);
    await MigrateAsync<CommunicationDbContext>(host.Services, logger);

    // Product-owned configuration (SYSTEM rows) is inserted or refreshed on every run.
    using (var scope = host.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<AppsControlSeeder>().SeedAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<PartiesSeeder>().SeedAsync(CancellationToken.None);
        logger.LogInformation("System data seeded");
    }

    // Every service has its own workflow (1f): services configured before get theirs.
    using (var scope = host.Services.CreateScope())
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using var work = await unitOfWork.BeginAsync(CancellationToken.None);
        var given = await WorkflowModule.BackfillServiceWorkflowsAsync(scope.ServiceProvider, CancellationToken.None);
        await work.CommitAsync(CancellationToken.None);
        if (given > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("{Count} service(s) given their workflow", given);
        }
    }

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
