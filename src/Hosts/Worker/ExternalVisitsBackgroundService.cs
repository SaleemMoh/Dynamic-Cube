using DynamicCube.Modules.Visits;

namespace DynamicCube.Worker;

/// <summary>Every minute: asks external inspection systems for the results of visits due a check.</summary>
public sealed class ExternalVisitsBackgroundService(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<ExternalVisitsBackgroundService> logger)
    : PeriodicJob(scopes, timeProvider, logger)
{
    protected override string Name => "External visit results";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken) => VisitsModule.PollExternalVisitsAsync(services, cancellationToken);
}
