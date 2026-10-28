using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DynamicCube.Platform.Outbox;

/// <summary>
/// Fetches due outbox messages under a lease (safe with several workers) and dispatches them to their handlers,
/// retrying failures with exponential backoff and dead-lettering after <see cref="OutboxOptions.MaxAttempts"/>.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    OutboxHandlerRegistry registry,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    private readonly OutboxOptions _options = options.Value;

    /// <summary>Processes one batch and returns the number of messages fetched.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var lockId = Guid.NewGuid();
        var batch = await LeaseBatchAsync(lockId, cancellationToken);

        foreach (var message in batch)
        {
            await ProcessAsync(message, lockId, cancellationToken);
        }

        return batch.Count;
    }

    private async Task<List<OutboxMessage>> LeaseBatchAsync(Guid lockId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lockedUntil = now + _options.LeaseDuration;
        var batchSize = _options.BatchSize;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        return await db.OutboxMessages
            .FromSql($"""
                UPDATE platform.outbox_messages
                   SET locked_until = {lockedUntil}, lock_id = {lockId}
                 WHERE id IN (
                       SELECT id FROM platform.outbox_messages
                        WHERE processed_at IS NULL
                          AND dead_lettered_at IS NULL
                          AND available_at <= {now}
                          AND (locked_until IS NULL OR locked_until < {now})
                        ORDER BY occurred_at
                        LIMIT {batchSize}
                          FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessAsync(OutboxMessage message, Guid lockId, CancellationToken cancellationToken)
    {
        string? error = null;
        try
        {
            if (!registry.TryGet(message.Type, out var dispatch))
            {
                throw new InvalidOperationException($"No outbox handler registered for '{message.Type}'.");
            }

            using var handlerScope = scopeFactory.CreateScope();
            await dispatch(handlerScope.ServiceProvider, message.Payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Any handler failure must be recorded and retried, never crash the worker.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            error = ex.ToString();
            logger.LogWarning(ex, "Outbox message {MessageId} of type {MessageType} failed (attempt {Attempt})", message.Id, message.Type, message.Attempts + 1);
        }

        await RecordOutcomeAsync(message, lockId, error, cancellationToken);
    }

    private async Task RecordOutcomeAsync(OutboxMessage message, Guid lockId, string? error, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var attempts = message.Attempts + 1;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var owned = db.OutboxMessages.Where(m => m.Id == message.Id && m.LockId == lockId);

        if (error is null)
        {
            await owned.ExecuteUpdateAsync(
                s => s.SetProperty(m => m.ProcessedAt, now)
                      .SetProperty(m => m.Attempts, attempts)
                      .SetProperty(m => m.LastError, (string?)null)
                      .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null),
                cancellationToken);
            return;
        }

        var deadLetter = attempts >= _options.MaxAttempts;
        var nextAttempt = now + RetryDelay(attempts);
        await owned.ExecuteUpdateAsync(
            s => s.SetProperty(m => m.Attempts, attempts)
                  .SetProperty(m => m.LastError, error)
                  .SetProperty(m => m.AvailableAt, nextAttempt)
                  .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null)
                  .SetProperty(m => m.DeadLetteredAt, deadLetter ? now : null),
            cancellationToken);

        if (deadLetter)
        {
            logger.LogError("Outbox message {MessageId} of type {MessageType} dead-lettered after {Attempts} attempts", message.Id, message.Type, attempts);
        }
    }

    internal TimeSpan RetryDelay(int attempts)
    {
        var factor = Math.Pow(2, Math.Max(0, attempts - 1));
        var delay = TimeSpan.FromTicks((long)Math.Min(_options.InitialRetryDelay.Ticks * factor, _options.MaxRetryDelay.Ticks));
        return delay;
    }
}
