using DynamicCube.Platform.Auditing;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Files;
using DynamicCube.Platform.Outbox;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DynamicCube.Platform;

public static class PlatformServiceCollectionExtensions
{
    /// <summary>Registers the platform kernel: clock, expressions, files, outbox, change-set policy and the platform DbContext.</summary>
    public static IServiceCollection AddPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<JsonLogicEvaluator>();

        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.TryAddSingleton<IFileStorage, FileSystemFileStorage>();

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.TryAddSingleton(serviceProvider =>
        {
            var registry = new OutboxHandlerRegistry();
            foreach (var registration in serviceProvider.GetServices<OutboxHandlerRegistration>())
            {
                registration.Apply(registry);
            }

            return registry;
        });
        services.TryAddSingleton<OutboxProcessor>();

        services.Configure<ChangeSetOptions>(configuration.GetSection(ChangeSetOptions.SectionName));
        services.TryAddScoped<IChangeApprovalPolicy, OptionsChangeApprovalPolicy>();

        services.TryAddScoped<AuditSaveChangesInterceptor>();
        services.TryAddSingleton<ISecretStore>(new ConfigurationSecretStore(configuration));

        services.AddModuleDbContext<PlatformDbContext>(configuration, PlatformSchemas.Platform);
        return services;
    }

    /// <summary>
    /// Registers a module DbContext on the shared PostgreSQL database with the standard conventions and the audit interceptor.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string historySchema)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // All module contexts of a scope share one connection, so a unit of work can span modules.
        services.TryAddScoped(_ =>
        {
            var connectionString = configuration.GetConnectionString(PostgresOptionsExtensions.ConnectionStringName);
            return string.IsNullOrWhiteSpace(connectionString)
                ? throw new InvalidOperationException($"Connection string '{PostgresOptionsExtensions.ConnectionStringName}' is not configured.")
                : new ModuleConnection(connectionString);
        });
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(new ModuleContextRegistration(typeof(TContext)));
        services.AddDbContext<TContext>((serviceProvider, options) =>
            options.UseDynamicCubePostgres(serviceProvider.GetRequiredService<ModuleConnection>().Connection, historySchema, typeof(TContext).Assembly.GetName().Name)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));
        return services;
    }

    /// <summary>Registers a handler for one outbox message type.</summary>
    public static IServiceCollection AddOutboxHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : IOutboxMessage
        where THandler : class, IOutboxHandler<TMessage>
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new OutboxHandlerRegistration(registry => registry.Register<TMessage, THandler>()));
        return services;
    }

    /// <summary>Registers change-set support for a module context together with its appliers.</summary>
    public static IServiceCollection AddChangeSets<TContext>(this IServiceCollection services)
        where TContext : ModuleDbContext
    {
        services.TryAddScoped<ChangeSetService<TContext>>();
        services.AddScoped<IChangeSetWorkflow>(sp => sp.GetRequiredService<ChangeSetService<TContext>>());
        return services;
    }

    /// <summary>Registers a configuration area so its approval policy can be set by the entity.</summary>
    public static IServiceCollection AddChangeArea(this IServiceCollection services, string code, string nameAr, string nameEn, bool requiresApprovalByDefault = true) =>
        services.AddSingleton(new ChangeAreaDefinition(code, nameAr, nameEn, requiresApprovalByDefault));

    /// <summary>Registers the resources, menus and built-in role privileges a module contributes to Apps Control.</summary>
    public static IServiceCollection AddSystemCatalog(this IServiceCollection services, SystemCatalogContribution contribution) =>
        services.AddSingleton(contribution);
}

/// <summary>Deferred registration applied to the <see cref="OutboxHandlerRegistry"/> when it is first resolved.</summary>
public sealed record OutboxHandlerRegistration(Action<OutboxHandlerRegistry> Apply);
