using DynamicCube.Modules.Reports.Changes;
using DynamicCube.Modules.Reports.Endpoints;
using DynamicCube.Modules.Reports.Persistence;
using DynamicCube.Modules.Reports.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Reports;

public static class ReportsModule
{
    public const string ApiPrefix = "/api/reports/v1";

    /// <summary>
    /// Registers the report builder (ADR-0045): reports and schedules (change sets), each report a resource of Apps Control,
    /// runs within the viewer's access, Excel and CSV exports, saved filters and scheduled email deliveries.
    /// </summary>
    public static IServiceCollection AddReports(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<ReportsDbContext>(configuration, ReportsDbContext.Schema);
        services.AddChangeSets<ReportsDbContext>();
        services.AddChangeArea(ReportChangeTypes.Area, "التقارير", "Reports");
        services.AddSystemCatalog(ReportsCatalog.Contribution);
        services.Configure<ReportsOptions>(configuration.GetSection(ReportsOptions.Section));

        AddApplier<ReportApplier>(services);
        AddApplier<ReportScheduleApplier>(services);
        services.AddScoped<IChangeSetPublishHook<ReportsDbContext>, ReportResourceSync>();
        services.AddScoped<ReportService>();
        services.AddScoped<ReportScheduler>();
        return services;
    }

    /// <summary>Internal host: reports, their runs and exports, the designer's preview.</summary>
    public static IEndpointRouteBuilder MapReportsInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).AddEndpointFilter(ReportEndpoints.Errors).WithTags("Reports").RequireAuthorization();
        ReportEndpoints.Map(group);
        return endpoints;
    }

    /// <summary>Worker: runs the report schedules that are due (every minute).</summary>
    public static Task<int> RunSchedulesAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ReportScheduler>().RunDueAsync(cancellationToken);

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<ReportsDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<ReportsDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Reports resources and menus contributed to Apps Control.</summary>
internal static class ReportsCatalog
{
    public const string MenuGroup = "REPORTS";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "reports",
        Resources =
        [
            new(ReportResources.Root, "التقارير", "Reports", "INTERNAL", null, [CatalogActions.View]),
            new(ReportResources.Config, "تصميم التقارير", "Report design", "INTERNAL", ReportResources.Root, CatalogActions.Edits),
            new(ReportResources.Schedules, "جدولة التقارير", "Report schedules", "INTERNAL", ReportResources.Root, CatalogActions.Edits),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "التقارير", "Reports", "INTERNAL", "analytics", 640),
        Menus =
        [
            new("reports.list", "التقارير", "Reports", "/reports", ReportResources.Root, "bar_chart", 10),
            new("reports.design", "تصميم التقارير", "Report design", "/reports/definitions", ReportResources.Config, "query_stats", 20),
            new("reports.schedules", "جدولة التقارير", "Report schedules", "/reports/schedules", ReportResources.Schedules, "schedule_send", 30),
            new("reports.deliveries", "سجل الإرسال المجدول", "Scheduled deliveries", "/reports/deliveries", ReportResources.Schedules, "outgoing_mail", 40),
        ],
    };
}
