using DynamicCube.Modules.Reports;

namespace DynamicCube.Worker;

/// <summary>Every minute: report schedules that are due send each recipient the report run with their own access (ADR-0045).</summary>
public sealed class ReportSchedulesBackgroundService(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<ReportSchedulesBackgroundService> logger)
    : PeriodicJob(scopes, timeProvider, logger)
{
    protected override string Name => "Report schedules";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken) => ReportsModule.RunSchedulesAsync(services, cancellationToken);
}
