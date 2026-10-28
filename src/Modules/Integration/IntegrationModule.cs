using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Endpoints;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Modules.Integration.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Integration;

public static class IntegrationModule
{
    public const string ApiPrefix = "/api/integration/v1";

    /// <summary>Named HttpClient used for provider calls (tests replace its primary handler).</summary>
    public const string HttpClientName = "dynamic-cube-integration";

    /// <summary>Registers the Integration Manager: configuration (change sets), the gateway and the call log.</summary>
    public static IServiceCollection AddIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<IntegrationDbContext>(configuration, IntegrationDbContext.Schema);
        services.AddChangeSets<IntegrationDbContext>();
        services.AddChangeArea(IntegrationChangeTypes.Area, "التكاملات", "Integrations");
        services.AddSystemCatalog(IntegrationCatalog.Contribution);
        services.AddMemoryCache();
        services.AddHttpClient(HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan);

        AddApplier<ProviderApplier>(services);
        AddApplier<ConnectionApplier>(services);
        AddApplier<EnvelopeProfileApplier>(services);
        AddApplier<OperationApplier>(services);
        services.AddScoped<IChangeSetPublishHook<IntegrationDbContext>, DefinitionCacheInvalidator>();

        services.AddSingleton<DefinitionCache>();
        services.AddSingleton<CircuitBreakers>();
        services.AddScoped<TokenProvider>();
        services.AddScoped<IntegrationGateway>();
        services.AddScoped<IIntegrationGateway>(sp => sp.GetRequiredService<IntegrationGateway>());
        return services;
    }

    /// <summary>Internal host: test console and call log (configuration is edited through Apps Control change sets).</summary>
    public static IEndpointRouteBuilder MapIntegrationInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(IntegrationEndpoints.Errors).WithTags("Integration");
        IntegrationEndpoints.Map(group);
        return endpoints;
    }

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<IntegrationDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<IntegrationDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Integration Manager resources and menus contributed to Apps Control.</summary>
internal static class IntegrationCatalog
{
    public const string MenuGroup = "INTEGRATION";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "integration",
        Resources =
        [
            new(IntegrationResources.Root, "إدارة التكاملات", "Integration Manager", "INTERNAL", null, [CatalogActions.View]),
            new(IntegrationResources.Config, "إعداد التكاملات", "Integration configuration", "INTERNAL", IntegrationResources.Root, CatalogActions.Edits),
            new(IntegrationResources.TestConsole, "تجربة العمليات", "Test console", "INTERNAL", IntegrationResources.Root, [CatalogActions.View, CatalogActions.Execute],
                RoleActions: new Dictionary<string, string[]> { [BuiltInRoles.ConfigChecker] = [CatalogActions.View, CatalogActions.Execute] }),
            new(IntegrationResources.CallLog, "سجل الاستدعاءات", "Call log", "INTERNAL", IntegrationResources.Root, [CatalogActions.View]),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "التكاملات", "Integrations", "INTERNAL", "hub", 800),
        Menus =
        [
            new("integration.operations", "العمليات", "Operations", "/integration/operations", IntegrationResources.Config, "api", 10),
            new("integration.connections", "الاتصالات", "Connections", "/integration/connections", IntegrationResources.Config, "cable", 20),
            new("integration.test-console", "تجربة العمليات", "Test console", "/integration/test-console", IntegrationResources.TestConsole, "science", 25),
            new("integration.envelopes", "أنماط الاستجابة", "Response envelopes", "/integration/envelopes", IntegrationResources.Config, "mail", 30),
            new("integration.providers", "الجهات المزودة", "Providers", "/integration/providers", IntegrationResources.Config, "corporate_fare", 40),
            new("integration.call-log", "سجل الاستدعاءات", "Call log", "/integration/call-log", IntegrationResources.CallLog, "history", 50),
        ],
    };
}
