using DynamicCube.Modules.Workflow;

namespace DynamicCube.Worker;

/// <summary>Every minute: workflow tasks and instances past their warning or due time turn amber or red.</summary>
public sealed class SlaBackgroundService(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<SlaBackgroundService> logger)
    : PeriodicJob(scopes, timeProvider, logger)
{
    protected override string Name => "SLA monitor";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken) => WorkflowModule.CheckSlaAsync(services, cancellationToken);
}
