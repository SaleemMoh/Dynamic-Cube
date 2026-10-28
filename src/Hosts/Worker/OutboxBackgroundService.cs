using DynamicCube.Platform.Outbox;
using Microsoft.Extensions.Options;

namespace DynamicCube.Worker;

/// <summary>Polls the outbox and delivers due messages. Several worker instances can run side by side.</summary>
public sealed class OutboxBackgroundService(
    OutboxProcessor processor,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox processor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Transient database failures must not stop the worker.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Outbox polling failed");
                processed = 0;
            }

            // Keep draining while there is work; otherwise wait for the next polling cycle.
            if (processed < options.Value.BatchSize)
            {
                try
                {
                    await Task.Delay(options.Value.PollingInterval, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
