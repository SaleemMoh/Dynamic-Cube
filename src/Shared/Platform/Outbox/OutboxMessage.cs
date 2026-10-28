namespace DynamicCube.Platform.Outbox;

/// <summary>
/// A message written in the same transaction as a business change and delivered later by the worker
/// (notifications, integration pushes). Delivery is at-least-once; handlers must be idempotent.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Stable message type key, e.g. <c>comm.send-notification</c>.</summary>
    public required string Type { get; set; }

    /// <summary>JSON payload.</summary>
    public required string Payload { get; set; }

    public string? CorrelationId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Earliest time the message may be (re)tried.</summary>
    public DateTimeOffset AvailableAt { get; set; }

    public int Attempts { get; set; }

    /// <summary>Lease held by a worker while processing.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    public Guid? LockId { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Set when the maximum number of attempts was reached; the message is no longer retried.</summary>
    public DateTimeOffset? DeadLetteredAt { get; set; }

    public string? LastError { get; set; }
}
