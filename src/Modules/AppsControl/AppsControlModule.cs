using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Endpoints;
using DynamicCube.Modules.AppsControl.Lookups;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Seeding;
using DynamicCube.Platform;
using DynamicCube.Platform.Calendars;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Lovs;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.AppsControl;

public static class AppsControlModule
{
    public const string ApiPrefix = "/api/apps-control/v1";

    /// <summary>Registers the Apps Control module.</summary>
    /// <param name="managesUsers">True on the internal host: employee accounts are provisioned and authorized here.</param>
    public static IServiceCollection AddAppsControl(this IServiceCollection services, IConfiguration configuration, bool managesUsers)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AppsControlOptions>(configuration.GetSection(AppsControlOptions.SectionName));
        services.AddModuleDbContext<AppsControlDbContext>(configuration, AppsControlDbContext.Schema);
        services.AddChangeSets<AppsControlDbContext>();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.ConfigureHttpJsonOptions(options => ChangeJson.Configure(options.SerializerOptions));

        services.AddSingleton(new AppsControlHostOptions(managesUsers));
        services.AddSingleton<AccessCache>();
        services.AddSingleton<SqlLovRunner>();

        // Areas whose approval policy the entity configures, with the initial policy of a new installation.
        services.AddChangeArea(AppsControlAreas.System, "النظام والجهة", "Installation & entity");
        services.AddChangeArea(AppsControlAreas.Organization, "الهيكل التنظيمي", "Organization structure");
        services.AddChangeArea(AppsControlAreas.Security, "المستخدمون والصلاحيات", "Users & privileges");
        services.AddChangeArea(AppsControlAreas.Menus, "القوائم", "Menus");
        services.AddChangeArea(AppsControlAreas.Lookups, "التصنيفات والقيم", "Classes & domains");
        services.AddChangeArea(AppsControlAreas.Lovs, "قوائم القيم", "Lists of values");
        services.AddChangeArea(AppsControlAreas.Settings, "الإعدادات", "Settings");
        services.AddChangeArea(AppsControlAreas.UiTexts, "نصوص الواجهة", "UI texts", requiresApprovalByDefault: false);

        services.AddSystemCatalog(SystemCatalog.Contribution);
        services.AddScoped<ChangeSetRouter>();
        AddApplier<InstallationApplier>(services);
        AddApplier<HolidayApplier>(services);
        AddApplier<EntityApplier>(services);
        AddApplier<AppBrandingApplier>(services);
        AddApplier<AppApplier>(services);
        AddApplier<BusinessAppApplier>(services);
        AddApplier<CompanyApplier>(services);
        AddApplier<BranchApplier>(services);
        AddApplier<OrgUnitTypeApplier>(services);
        AddApplier<OrgUnitApplier>(services);
        AddApplier<PositionApplier>(services);
        AddApplier<ResourceApplier>(services);
        AddApplier<RoleApplier>(services);
        AddApplier<UserApplier>(services);
        AddApplier<MenuGroupApplier>(services);
        AddApplier<MenuApplier>(services);
        AddApplier<ClassApplier>(services);
        AddApplier<DomainApplier>(services);

        // Types kept as values of a class, each under its own change type and screen.
        foreach (var type in DomainBackedTypes.All)
        {
            services.AddScoped(sp => new DomainBackedApplier(sp.GetRequiredService<AppsControlDbContext>(), type, sp.GetServices<IDomainRules>()));
            services.AddScoped<IConfigApplier>(sp => sp.GetServices<DomainBackedApplier>().Single(a => a.EntityType == type.EntityType));
            services.AddScoped<IChangeApplier<AppsControlDbContext>>(sp => sp.GetServices<DomainBackedApplier>().Single(a => a.EntityType == type.EntityType));
        }
        AddApplier<DataObjectApplier>(services);
        AddApplier<LovApplier>(services);
        AddApplier<SettingApplier>(services);
        AddApplier<UiTextApplier>(services);
        AddApplier<ChangeAreaApplier>(services);

        // Replaces the configuration-only default registered by the platform.
        services.AddScoped<IChangeApprovalPolicy, AppsControlApprovalPolicy>();

        services.AddScoped<IChangeSetPublishHook<AppsControlDbContext>, ClassViewGenerator>();
        services.AddScoped<IChangeSetPublishHook<AppsControlDbContext>, AccessCacheInvalidator>();

        services.AddScoped<AccessResolver>();
        services.AddScoped<IAccessChecker, AccessChecker>();
        services.AddScoped<IStaffDirectory, StaffDirectory>();
        services.AddScoped<IWorkScope, WorkScopes>();
        services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();
        services.AddScoped<IConfigScopes, ConfigScopes>();
        services.AddScoped<ILookupDirectory, LookupDirectory>();
        services.AddScoped<IWorkingCalendarProvider, WorkingCalendarProvider>();
        services.AddScoped<MenuService>();
        services.AddScoped<GrantResolver>();
        services.AddScoped<LovService>();
        services.AddScoped<ILovValidator, LovValidator>();
        services.AddScoped<DataScopeService>();
        services.AddScoped<AppsControlSeeder>();

        // Sign-in accounts: maintained through the identity provider's admin API when one is configured.
        services.Configure<IdentityOptions>(configuration.GetSection(IdentityOptions.SectionName));
        services.AddHttpClient(KeycloakAccountDirectory.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.TryAddSingleton(TimeProvider.System);
        if (string.Equals(configuration[$"{IdentityOptions.SectionName}:Provider"], "Keycloak", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IAccountDirectory, KeycloakAccountDirectory>();
        }
        else
        {
            services.AddScoped<IAccountDirectory, UnmanagedAccountDirectory>();
        }

        services.AddScoped<AccountService>();

        // Applicants' portal accounts: another realm, maintained the same way (user name = national ID).
        var portal = new IdentityOptions
        {
            BaseUrl = configuration[$"{IdentityOptions.SectionName}:BaseUrl"],
            Realm = "dynamic-cube-external",
            ClientId = "dynamic-cube-portal-accounts",
            SecretName = "KEYCLOAK_PORTAL_ADMIN",
        };
        configuration.GetSection(IdentityOptions.PortalSectionName).Bind(portal);
        if (string.Equals(portal.Provider, "Keycloak", StringComparison.OrdinalIgnoreCase))
        {
            services.AddKeyedScoped<IAccountDirectory>(PortalAccountService.DirectoryKey, (sp, _) => new KeycloakAccountDirectory(
                sp.GetRequiredService<IHttpClientFactory>(), Options.Create(portal), sp.GetRequiredService<ISecretStore>(), sp.GetRequiredService<TimeProvider>()));
        }
        else
        {
            services.AddKeyedScoped<IAccountDirectory, UnmanagedAccountDirectory>(PortalAccountService.DirectoryKey);
        }

        services.AddScoped<PortalAccountService>();
        return services;
    }

    /// <summary>Internal host: profile, menus, privileges, LOVs, configuration reads and the change-set workflow.</summary>
    public static IEndpointRouteBuilder MapAppsControlInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(AppsControlFilters.ActiveUser).WithTags("Apps Control");
        PublicEndpoints.Map(group, external: false);
        MeEndpoints.Map(group);
        AdminEndpoints.Map(group);
        return endpoints;
    }

    /// <summary>External host: only public LOVs, public settings and UI texts.</summary>
    public static IEndpointRouteBuilder MapAppsControlExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().WithTags("Apps Control");
        PublicEndpoints.Map(group, external: true);
        return endpoints;
    }

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<AppsControlDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<AppsControlDbContext>>(sp => sp.GetRequiredService<T>());
    }
}
