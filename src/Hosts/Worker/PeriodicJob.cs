namespace DynamicCube.Worker;

/// <summary>Runs a job in a fresh scope at a fixed interval; a failed run is logged and the next one goes ahead.</summary>
public abstract partial class PeriodicJob(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger logger) : BackgroundService
{
    protected abstract string Name { get; }

    protected abstract TimeSpan Interval { get; }

    protected abstract Task RunAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var job = Name;
        Started(logger, job);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await RunAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Transient database failures must not stop the worker.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Failed(logger, ex, job);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "{Job} started")]
    private static partial void Started(ILogger logger, string job);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Job} failed")]
    private static partial void Failed(ILogger logger, Exception exception, string job);
}
