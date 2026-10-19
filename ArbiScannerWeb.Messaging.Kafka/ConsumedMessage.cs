using Confluent.Kafka;

namespace ArbiScannerWeb.Messaging.Kafka;

public sealed record ConsumedMessage(
    string Topic,
    int Partition,
    long Offset,
    string Key,
    byte[] Value,
    Headers Headers,
    EnvelopeMetadata Metadata,
    DateTime TimestampUtc);

/// <summary>Processes one message. Resolved from a fresh DI scope per message, so scoped services
/// (DbContexts, ...) are safe to inject. Throw to trigger a retry; throw
/// <see cref="NonRetryableMessageException"/> for a message that can never succeed.</summary>
public interface IKafkaMessageHandler
{
    Task HandleAsync(ConsumedMessage message, CancellationToken cancellationToken);
}

/// <summary>A message that retrying cannot fix (e.g. undecodable payload). Goes straight to the dead-letter topic.</summary>
public sealed class NonRetryableMessageException : Exception
{
    public NonRetryableMessageException()
    {
    }

    public NonRetryableMessageException(string message)
        : base(message)
    {
    }

    public NonRetryableMessageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
