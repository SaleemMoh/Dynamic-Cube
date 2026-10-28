using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Platform.Outbox;

/// <summary>Maps message type keys to their payload type and handler.</summary>
public sealed class OutboxHandlerRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, string, CancellationToken, Task>> _dispatchers = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> MessageTypes => _dispatchers.Keys;

    public void Register<TMessage, THandler>()
        where TMessage : IOutboxMessage
        where THandler : IOutboxHandler<TMessage>
    {
        if (!_dispatchers.TryAdd(TMessage.MessageType, Dispatch))
        {
            throw new InvalidOperationException($"An outbox handler for '{TMessage.MessageType}' is already registered.");
        }

        static Task Dispatch(IServiceProvider services, string payload, CancellationToken cancellationToken)
        {
            var message = JsonSerializer.Deserialize<TMessage>(payload, OutboxExtensions.SerializerOptions)
                ?? throw new InvalidOperationException($"Outbox payload for '{TMessage.MessageType}' deserialized to null.");
            var handler = services.GetRequiredService<THandler>();
            return handler.HandleAsync(message, cancellationToken);
        }
    }

    internal bool TryGet(string messageType, out Func<IServiceProvider, string, CancellationToken, Task> dispatcher) =>
        _dispatchers.TryGetValue(messageType, out dispatcher!);
}
