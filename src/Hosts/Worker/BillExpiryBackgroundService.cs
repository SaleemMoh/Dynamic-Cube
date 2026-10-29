using DynamicCube.Modules.Payments;

namespace DynamicCube.Worker;

/// <summary>Every 5 minutes: unpaid bills past their due date expire, and requests waiting for them move on.</summary>
public sealed class BillExpiryBackgroundService(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<BillExpiryBackgroundService> logger)
    : PeriodicJob(scopes, timeProvider, logger)
{
    protected override string Name => "Bill expiry";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken) => PaymentsModule.ExpireBillsAsync(services, cancellationToken);
}
