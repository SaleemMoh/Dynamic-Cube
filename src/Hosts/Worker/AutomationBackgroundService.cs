using DynamicCube.Modules.Workflow;

namespace DynamicCube.Worker;

/// <summary>Runs the workflow automation rules on open tasks (assign, remind, escalate) every minute.</summary>
public sealed class AutomationBackgroundService(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<AutomationBackgroundService> logger)
    : PeriodicJob(scopes, timeProvider, logger)
{
    protected override string Name => "Workflow automation";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken) => WorkflowModule.RunAutomationAsync(services, cancellationToken);
}
