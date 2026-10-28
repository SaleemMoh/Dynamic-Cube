using DynamicCube.Modules.Workflow;

namespace DynamicCube.Worker;

/// <summary>Every minute: workflow tasks and instances past their warning or due time turn amber or red.</summary>
public sealed class SlaBackgroundService(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<SlaBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA monitor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await WorkflowModule.CheckSlaAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Transient database failures must not stop the worker.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "SLA check failed");
            }

            try
            {
                await Task.Delay(Interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
