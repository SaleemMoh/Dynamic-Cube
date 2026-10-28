using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Endpoints;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Modules.Parties.Seeding;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Parties;

public static class PartiesModule
{
    public const string ApiPrefix = "/api/party/v1";

    /// <summary>Registers the Party Registry. Needs the Integration Manager (<see cref="Platform.Integration.IIntegrationGateway"/>).</summary>
    public static IServiceCollection AddParties(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PartiesOptions>(configuration.GetSection(PartiesOptions.SectionName));
        services.AddModuleDbContext<PartiesDbContext>(configuration, PartiesDbContext.Schema);
        services.AddChangeSets<PartiesDbContext>();
        services.AddChangeArea(PartyChangeTypes.Area, "سجل المتعاملين", "Party registry");
        services.AddSystemCatalog(PartiesCatalog.Contribution);
        services.AddHttpContextAccessor();

        AddApplier<RelationshipTypeApplier>(services);
        AddApplier<ProfileFieldApplier>(services);
        AddApplier<CredentialTypeApplier>(services);
        AddApplier<SanctionTypeApplier>(services);
        AddApplier<ContextSourceApplier>(services);

        services.AddScoped<IApplicantIdentity, ClaimsApplicantIdentity>();
        services.AddScoped<PartyDirectory>();
        services.AddScoped<ContextService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<PartyAdminService>();
        services.AddScoped<IActingPartyAccessor, ActingPartyAccessor>();
        services.AddScoped<IPartyFacts, PartyFactsProvider>();
        services.AddScoped<PartiesSeeder>();
        return services;
    }

    /// <summary>Internal host: the registry for employees.</summary>
    public static IEndpointRouteBuilder MapPartiesInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(PartyEndpoints.Errors).WithTags("Party Registry");
        PartyEndpoints.MapInternal(group);
        return endpoints;
    }

    /// <summary>External host: acting contexts of the signed-in applicant.</summary>
    public static IEndpointRouteBuilder MapPartiesExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(PartyEndpoints.Errors).WithTags("Party Registry");
        PartyEndpoints.MapExternal(group);
        return endpoints;
    }

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<PartiesDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<PartiesDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Party Registry resources and menus contributed to Apps Control.</summary>
internal static class PartiesCatalog
{
    public const string MenuGroup = "PARTY";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "party",
        Resources =
        [
            new(PartyResources.Root, "سجل المتعاملين", "Party registry", "INTERNAL", null, [CatalogActions.View]),
            new(PartyResources.Parties, "المتعاملون", "Parties", "INTERNAL", PartyResources.Root, [CatalogActions.View, CatalogActions.Create, CatalogActions.Edit]),
            new(PartyResources.Sanctions, "الجزاءات", "Sanctions", "INTERNAL", PartyResources.Root, [CatalogActions.View, CatalogActions.Create, CatalogActions.Edit]),
            new(PartyResources.Config, "إعداد سجل المتعاملين", "Party registry configuration", "INTERNAL", PartyResources.Root, CatalogActions.Edits),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "سجل المتعاملين", "Party registry", "INTERNAL", "badge", 700),
        Menus =
        [
            new("party.parties", "المتعاملون", "Parties", "/party/parties", PartyResources.Parties, "groups", 10),
            new("party.relationship-types", "أنواع العلاقات", "Relationship types", "/party/relationship-types", PartyResources.Config, "share", 20),
            new("party.context-sources", "مصادر صفة التمثيل", "Acting context sources", "/party/context-sources", PartyResources.Config, "switch_account", 30),
            new("party.profile-fields", "بيانات الملف", "Profile fields", "/party/profile-fields", PartyResources.Config, "assignment_ind", 40),
            new("party.credential-types", "أنواع الشهادات", "Credential types", "/party/credential-types", PartyResources.Config, "workspace_premium", 50),
            new("party.sanction-types", "أنواع الجزاءات", "Sanction types", "/party/sanction-types", PartyResources.Config, "gavel", 60),
        ],
    };
}
