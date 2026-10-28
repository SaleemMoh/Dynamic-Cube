using DynamicCube.Modules.Documents;

namespace DynamicCube.Worker;

/// <summary>Every 15 minutes: documents past their expiry date become expired.</summary>
public sealed class DocumentDatesBackgroundService(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<DocumentDatesBackgroundService> logger)
    : PeriodicJob(scopes, timeProvider, logger)
{
    protected override string Name => "Document dates";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken) => DocumentsModule.CheckDatesAsync(services, cancellationToken);
}
