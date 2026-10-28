using DynamicCube.Modules.Decisions.Changes;
using DynamicCube.Modules.Decisions.Endpoints;
using DynamicCube.Modules.Decisions.Persistence;
using DynamicCube.Modules.Decisions.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Decisions;

public static class DecisionsModule
{
    public const string ApiPrefix = "/api/decisions/v1";

    /// <summary>
    /// Registers decisions (ADR-0014): versioned rule sets (change sets), <see cref="IDecisionService"/> with an evaluation
    /// log, and the built-in <c>RULES</c> engine. Other engines register as <see cref="IDecisionEngine"/> services.
    /// </summary>
    public static IServiceCollection AddDecisions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<DecisionsDbContext>(configuration, DecisionsDbContext.Schema);
        services.AddChangeSets<DecisionsDbContext>();
        services.AddChangeArea(DecisionChangeTypes.Area, "قواعد القرار", "Decision rules");
        services.AddSystemCatalog(DecisionsCatalog.Contribution);

        AddApplier<RuleSetApplier>(services);
        AddApplier<RuleSetVersionApplier>(services);
        services.AddSingleton<IDecisionEngine, RulesEngine>();
        services.AddScoped<DecisionService>();
        services.AddScoped<IDecisionService>(sp => sp.GetRequiredService<DecisionService>());
        return services;
    }

    public static IEndpointRouteBuilder MapDecisionsInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(DecisionEndpoints.Errors).WithTags("Decisions");
        DecisionEndpoints.MapInternal(group);
        return endpoints;
    }

    public static IEndpointRouteBuilder MapDecisionsExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).AddEndpointFilter(DecisionEndpoints.Errors).WithTags("Decisions");
        DecisionEndpoints.MapPublic(group);
        return endpoints;
    }

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<DecisionsDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<DecisionsDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Decision resources and menus contributed to Apps Control.</summary>
internal static class DecisionsCatalog
{
    public const string MenuGroup = "DECISIONS";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "decisions",
        Resources =
        [
            new(DecisionResources.Config, "قواعد القرار", "Decision rules", "INTERNAL", null, CatalogActions.Edits),
            new(DecisionResources.Evaluations, "نتائج التقييم", "Evaluations", "INTERNAL", null, [CatalogActions.View]),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "قواعد القرار", "Decisions", "INTERNAL", "rule", 520),
        Menus =
        [
            new("decisions.rule-sets", "مجموعات القواعد", "Rule sets", "/decisions", DecisionResources.Config, "rule_folder", 10),
            new("decisions.versions", "إصدارات القواعد", "Rule-set versions", "/decisions/versions", DecisionResources.Config, "history_edu", 20),
        ],
    };
}
