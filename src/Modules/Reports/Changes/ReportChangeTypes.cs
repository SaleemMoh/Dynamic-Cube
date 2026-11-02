namespace DynamicCube.Modules.Reports.Changes;

/// <summary>Entity types, change-set area and guarding resources of reports (ADR-0045).</summary>
public static class ReportChangeTypes
{
    public const string Report = "rpt.report";

    public const string Schedule = "rpt.schedule";

    public const string Dashboard = "rpt.dashboard";

    public const string Area = "rpt.reports";
}

public static class ReportResources
{
    /// <summary>The reports list; each report is a child resource of it.</summary>
    public const string Root = "reports";

    /// <summary>Designing reports.</summary>
    public const string Config = "reports.config";

    /// <summary>Scheduled deliveries and their log.</summary>
    public const string Schedules = "reports.schedules";

    /// <summary>Designing shared dashboards (ADR-0047).</summary>
    public const string Dashboards = "reports.dashboards";

    /// <summary>The resource of one report: roles grant VIEW (and EXPORT) on it; menus open it.</summary>
    public static string Of(string reportCode) => "reports.r." + reportCode.ToLowerInvariant();

    /// <summary>The resource of one shared dashboard: roles grant VIEW on it; menus open it.</summary>
    public static string Dashboard(string dashboardCode) => "reports.d." + dashboardCode.ToLowerInvariant();
}
