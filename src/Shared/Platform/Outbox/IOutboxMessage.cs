namespace DynamicCube.Platform.Outbox;

/// <summary>A payload type that can be written to the outbox.</summary>
public interface IOutboxMessage
{
    /// <summary>Stable type key; never derived from the CLR name so renames do not break pending messages.</summary>
    static abstract string MessageType { get; }
}

/// <summary>Delivers one outbox message type. Must be idempotent.</summary>
public interface IOutboxHandler<in TMessage>
    where TMessage : IOutboxMessage
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
