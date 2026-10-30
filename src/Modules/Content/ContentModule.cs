using DynamicCube.Modules.Content.Changes;
using DynamicCube.Modules.Content.Endpoints;
using DynamicCube.Modules.Content.Persistence;
using DynamicCube.Modules.Content.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Content;

public static class ContentModule
{
    public const string ApiPrefix = "/api/content/v1";

    /// <summary>
    /// Registers portal content: pages and frequently asked questions (change sets), read anonymously by the portal. The
    /// portal home page (banner, statistics tiles, help links) is the <c>PORTAL_HOME</c> setting of Apps Control.
    /// </summary>
    public static IServiceCollection AddContent(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<ContentDbContext>(configuration, ContentDbContext.Schema);
        services.AddChangeSets<ContentDbContext>();
        services.AddChangeArea(ContentChangeTypes.Area, "محتوى البوابة", "Portal content");
        services.AddSystemCatalog(ContentCatalog.Contribution);

        AddApplier<ContentPageApplier>(services);
        AddApplier<ContentPageVersionApplier>(services);
        AddApplier<FaqApplier>(services);
        services.AddScoped<ContentService>();
        services.AddScoped<FeedbackService>();
        return services;
    }

    /// <summary>External host: what the portal shows.</summary>
    public static IEndpointRouteBuilder MapContentExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).AddEndpointFilter(ContentEndpoints.Errors).WithTags("Content");
        ContentEndpoints.MapPublic(group);
        return endpoints;
    }

    /// <summary>Internal host: visitors' feedback for the content team.</summary>
    public static IEndpointRouteBuilder MapContentInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).AddEndpointFilter(ContentEndpoints.Errors).WithTags("Content");
        ContentEndpoints.MapInternal(group);
        return endpoints;
    }

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<ContentDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<ContentDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Content resources and menus contributed to Apps Control.</summary>
internal static class ContentCatalog
{
    public const string MenuGroup = "CONTENT";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "content",
        Resources =
        [
            new(ContentResources.Config, "محتوى البوابة", "Portal content", "INTERNAL", null, CatalogActions.Edits),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "محتوى البوابة", "Portal content", "INTERNAL", "web", 680),
        Menus =
        [
            new("content.home", "الصفحة الرئيسية للبوابة", "Portal home page", "/content/home", ContentResources.Config, "home", 10),
            new("content.faqs", "الأسئلة الشائعة", "FAQs", "/content/faqs", ContentResources.Config, "quiz", 20),
            new("content.pages", "الصفحات", "Pages", "/content/pages", ContentResources.Config, "article", 30),
            new("content.feedback", "آراء الزوار", "Visitor feedback", "/content/feedback", ContentResources.Config, "reviews", 40),
        ],
    };
}
