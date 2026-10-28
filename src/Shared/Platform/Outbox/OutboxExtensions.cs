using System.Diagnostics;
using System.Text.Json;
using DynamicCube.Platform.Persistence;

namespace DynamicCube.Platform.Outbox;

public static class OutboxExtensions
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Adds a message to the outbox. It is persisted by the next <c>SaveChanges</c> of the same context,
    /// i.e. atomically with the business change.
    /// </summary>
    public static OutboxMessage Enqueue<TMessage>(this ModuleDbContext context, TMessage message, TimeProvider timeProvider)
        where TMessage : IOutboxMessage
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();
        var outboxMessage = new OutboxMessage
        {
            Type = TMessage.MessageType,
            Payload = JsonSerializer.Serialize(message, SerializerOptions),
            CorrelationId = Activity.Current?.TraceId.ToString(),
            OccurredAt = now,
            AvailableAt = now,
        };
        context.OutboxMessages.Add(outboxMessage);
        return outboxMessage;
    }
}
