using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Documents.Endpoints;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Modules.Documents.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DynamicCube.Modules.Documents;

public static class DocumentsModule
{
    public const string ApiPrefix = "/api/documents/v1";

    /// <summary>
    /// Registers the issued-document registry: document types and templates (change sets), issuing (<see cref="IDocumentIssuer"/>),
    /// printing through Gotenberg, verification and expiry. Issuing needs the Party Registry.
    /// </summary>
    public static IServiceCollection AddDocuments(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<DocumentsDbContext>(configuration, DocumentsDbContext.Schema);
        services.AddChangeSets<DocumentsDbContext>();
        services.AddChangeArea(DocumentChangeTypes.Area, "المستندات", "Documents");
        services.AddSystemCatalog(DocumentsCatalog.Contribution);
        services.Configure<DocumentsOptions>(configuration.GetSection(DocumentsOptions.SectionName));

        AddApplier<DocumentTemplateApplier>(services);
        AddApplier<DocumentTypeApplier>(services);
        services.TryAddSingleton<LiquidRenderer>();
        services.AddHttpClient(GotenbergPdfRenderer.HttpClientName);
        services.TryAddSingleton<IPdfRenderer, GotenbergPdfRenderer>();
        services.AddSingleton<DocumentRenderer>();
        services.AddScoped<DocumentIssuer>();
        services.AddScoped<IDocumentIssuer>(sp => sp.GetRequiredService<DocumentIssuer>());
        services.AddScoped<DocumentRegistry>();
        services.AddScoped<DocumentDates>();
        services.AddScoped<RenderDocumentHandler>();
        services.AddOutboxHandler<RenderDocumentMessage, RenderDocumentHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapDocumentsInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(DocumentEndpoints.Errors).WithTags("Documents");
        DocumentEndpoints.MapInternal(group);
        return endpoints;
    }

    /// <summary>External host: the applicant's documents (signed in) and public verification (anonymous).</summary>
    public static IEndpointRouteBuilder MapDocumentsExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).AddEndpointFilter(DocumentEndpoints.Errors).WithTags("Documents");
        DocumentEndpoints.MapPublic(group);
        DocumentEndpoints.MapExternal(group.MapGroup(string.Empty).RequireAuthorization());
        return endpoints;
    }

    /// <summary>Expires documents past their date (called periodically by the worker); returns how many expired.</summary>
    public static Task<int> CheckDatesAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<DocumentDates>().RunAsync(cancellationToken);

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<DocumentsDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<DocumentsDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Document resources and menus contributed to Apps Control.</summary>
internal static class DocumentsCatalog
{
    public const string MenuGroup = "DOCUMENTS";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "documents",
        Resources =
        [
            new(DocumentResources.Registry, "سجل المستندات", "Document registry", "INTERNAL", null, [CatalogActions.View, CatalogActions.Edit]),
            new(DocumentResources.Config, "إعداد المستندات", "Document configuration", "INTERNAL", null, CatalogActions.Edits),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "المستندات", "Documents", "INTERNAL", "workspace_premium", 650),
        Menus =
        [
            new("documents.registry", "المستندات الصادرة", "Issued documents", "/documents", DocumentResources.Registry, "verified", 10),
            new("documents.types", "أنواع المستندات", "Document types", "/documents/types", DocumentResources.Config, "badge", 20),
            new("documents.templates", "قوالب الطباعة", "Print templates", "/documents/templates", DocumentResources.Config, "draw", 30),
        ],
    };
}
