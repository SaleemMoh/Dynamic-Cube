using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Endpoints;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Services;
using DynamicCube.Platform.Workflow;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services;

public static class ServicesModule
{
    public const string ApiPrefix = "/api/services/v1";

    /// <summary>
    /// Registers Services: service definitions, the question bank, versions and requests. Needs the Party Registry
    /// (<see cref="Platform.Parties.IPartyFacts"/>), Apps Control (<see cref="Platform.Lovs.ILovValidator"/>) and the
    /// workflow engine (<see cref="IWorkflowRuntime"/>), which processes submitted requests.
    /// </summary>
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<ServicesDbContext>(configuration, ServicesDbContext.Schema);
        services.AddChangeSets<ServicesDbContext>();
        services.AddChangeArea(ServiceChangeTypes.Area, "الخدمات", "Services");
        services.AddSystemCatalog(ServicesCatalog.Contribution);

        AddApplier<ServiceCategoryApplier>(services);
        AddApplier<ServiceDefinitionApplier>(services);
        AddApplier<QuestionApplier>(services);
        AddApplier<ServiceVersionApplier>(services);
        services.AddScoped<IChangeSetPublishHook<ServicesDbContext>, VersionCompiler>();
        services.AddScoped<IChangeSetPublishHook<ServicesDbContext>, ServiceWorkflowProvisioner>();
        services.AddScoped<IServiceCatalog, ServiceCatalog>();
        services.AddSingleton<ServiceCatalogCache>();
        services.AddScoped<Eligibility>();
        services.AddScoped<FeeCalculator>();
        services.AddScoped<FormValidator>();
        services.AddScoped<RequestService>();
        services.AddScoped<IWorkflowSubject, RequestSubject>();
        services.AddScoped<RequestAdminService>();
        services.AddScoped<PortalCatalogService>();
        services.AddCommunicationEvents(RequestNotifications.Types);
        return services;
    }

    /// <summary>Internal host: requests (read) and version previews; configuration is edited through change sets.</summary>
    public static IEndpointRouteBuilder MapServicesInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(ServiceEndpoints.Errors).WithTags("Services");
        ServiceEndpoints.MapInternal(group);
        return endpoints;
    }

    /// <summary>External host: the public catalog and service cards; catalog, drafts and submission for the signed-in applicant and their acting party.</summary>
    public static IEndpointRouteBuilder MapServicesExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(ServiceEndpoints.Errors).WithTags("Services");
        ServiceEndpoints.MapExternal(group);
        ServiceEndpoints.MapPortal(group);
        return endpoints;
    }

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<ServicesDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<ServicesDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Services resources and menus contributed to Apps Control.</summary>
internal static class ServicesCatalog
{
    public const string MenuGroup = "SERVICES";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "services",
        Resources =
        [
            new(ServiceResources.Root, "الخدمات", "Services", "INTERNAL", null, [CatalogActions.View]),
            new(ServiceResources.Config, "إعداد الخدمات", "Service configuration", "INTERNAL", ServiceResources.Root, CatalogActions.Edits),
            new(ServiceResources.Requests, "الطلبات", "Requests", "INTERNAL", ServiceResources.Root, [CatalogActions.View]),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "الخدمات", "Services", "INTERNAL", "design_services", 600),
        Menus =
        [
            new("services.requests", "الطلبات", "Requests", "/services/requests", ServiceResources.Requests, "inbox", 10),
            new("services.catalog", "الخدمات", "Services", "/services/catalog", ServiceResources.Config, "design_services", 20),
            new("services.versions", "إصدارات الخدمات", "Service versions", "/services/versions", ServiceResources.Config, "history_edu", 30),
            new("services.questions", "بنك الأسئلة", "Question bank", "/services/questions", ServiceResources.Config, "quiz", 40),
            new("services.categories", "تصنيفات الخدمات", "Service categories", "/services/categories", ServiceResources.Config, "category", 50),
        ],
    };
}
