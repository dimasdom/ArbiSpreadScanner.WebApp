namespace ArbiScannerWeb.Abstractions.Messaging;

public static class MessagingBackends
{
    public const string RabbitMq = "rabbitmq";

    public const string Kafka = "kafka";
}

/// <summary>Records which messages each backend delivered, per minute, so the RabbitMQ -> Kafka migration can prove
/// both brokers carried the same events before any consumer is cut over. Recording must never affect processing.</summary>
public interface IParityRecorder
{
    /// <param name="backend">One of <see cref="MessagingBackends"/>.</param>
    /// <param name="key">The spread key (partition key on Kafka).</param>
    /// <param name="messageId">Identical on both backends for the same event.</param>
    /// <param name="producedAt">Producer timestamp, identical on both backends, used to pick the minute bucket.</param>
    Task RecordAsync(string backend, string key, string messageId, DateTimeOffset producedAt);
}

/// <summary>Used when parity tracking is off (every mode except <c>Shadow</c>).</summary>
public sealed class NoOpParityRecorder : IParityRecorder
{
    public Task RecordAsync(string backend, string key, string messageId, DateTimeOffset producedAt) => Task.CompletedTask;
}
