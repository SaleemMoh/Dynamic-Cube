using DynamicCube.Modules.Visits.Changes;
using DynamicCube.Modules.Visits.Endpoints;
using DynamicCube.Modules.Visits.Persistence;
using DynamicCube.Modules.Visits.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Visits;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Visits;

public static class VisitsModule
{
    public const string ApiPrefix = "/api/visits/v1";

    /// <summary>
    /// Registers field visits: visit types (change sets), visits requested by the workflow's visit steps and carried out in
    /// the field app, by an external inspection system (booked through Integration Manager, results polled or notified)
    /// or recorded by staff, and <see cref="IVisits"/> for the workflow.
    /// </summary>
    public static IServiceCollection AddVisits(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<VisitsDbContext>(configuration, VisitsDbContext.Schema);
        services.AddChangeSets<VisitsDbContext>();
        services.AddChangeArea(VisitChangeTypes.Area, "الزيارات الميدانية", "Field visits");
        services.AddSystemCatalog(VisitsCatalog.Contribution);

        services.AddScoped<VisitTypeApplier>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<VisitTypeApplier>());
        services.AddScoped<IChangeApplier<VisitsDbContext>>(sp => sp.GetRequiredService<VisitTypeApplier>());
        services.AddScoped<VisitService>();
        services.AddScoped<IVisits>(sp => sp.GetRequiredService<VisitService>());
        services.AddScoped<VisitRegistry>();
        services.AddScoped<FieldVisits>();
        services.AddScoped<ExternalVisits>();
        services.AddCommunicationEvents(VisitNotifications.Types);
        return services;
    }

    public static IEndpointRouteBuilder MapVisitsInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(VisitEndpoints.Errors).WithTags("Visits");
        VisitEndpoints.MapInternal(group);
        return endpoints;
    }

    /// <summary>External host: the external inspection systems' result notifications.</summary>
    public static IEndpointRouteBuilder MapVisitsExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).AddEndpointFilter(VisitEndpoints.Errors).WithTags("Visits");
        VisitEndpoints.MapCallbacks(group);
        return endpoints;
    }

    /// <summary>Asks external systems for the results of visits due a check (called periodically by the worker).</summary>
    public static Task<int> PollExternalVisitsAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ExternalVisits>().PollAsync(cancellationToken);
}

/// <summary>Visit resources and menus contributed to Apps Control.</summary>
internal static class VisitsCatalog
{
    public const string MenuGroup = "VISITS";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "visits",
        Classes =
        [
            new CatalogClass("VIS_MODE", "طرق تنفيذ الزيارات", "Visit modes", [],
            [
                new CatalogValue("FIELD", "ميدانية", "Field"),
                new CatalogValue("EXTERNAL", "جهة معاينة خارجية", "External body"),
                new CatalogValue("MANUAL", "تسجيل يدوي", "Manual"),
            ]),
            new CatalogClass("VIS_OUTCOME_KIND", "أنواع نتائج الزيارات", "Visit outcome kinds", [],
            [
                new CatalogValue("PASS", "مطابق", "Pass"),
                new CatalogValue("FAIL", "غير مطابق", "Fail"),
                new CatalogValue("OTHER", "أخرى", "Other"),
            ]),
        ],
        Resources =
        [
            new(VisitResources.Registry, "الزيارات الميدانية", "Field visits", "INTERNAL", null, [CatalogActions.View, CatalogActions.Edit]),
            new(VisitResources.Config, "أنواع الزيارات", "Visit types", "INTERNAL", null, CatalogActions.Edits),
            new(VisitResources.Field, "زياراتي (تطبيق الزيارات)", "My visits (field app)", "FIELD", null, [CatalogActions.View]),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "الزيارات الميدانية", "Field visits", "INTERNAL", "location_on", 670),
        Menus =
        [
            new("visits.registry", "الزيارات", "Visits", "/visits", VisitResources.Registry, "fact_check", 10),
            new("visits.types", "أنواع الزيارات", "Visit types", "/visits/types", VisitResources.Config, "category", 20),
        ],
    };
}
