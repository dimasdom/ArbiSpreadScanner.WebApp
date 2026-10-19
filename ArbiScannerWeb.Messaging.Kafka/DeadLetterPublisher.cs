using System.Globalization;
using System.Text;
using Confluent.Kafka;

namespace ArbiScannerWeb.Messaging.Kafka;

public interface IDeadLetterPublisher
{
    Task PublishAsync(ConsumedMessage message, Exception failure, int attempts, CancellationToken cancellationToken);
}

/// <summary>Publishes a poison message to <c>&lt;topic&gt;.dlq</c> with the same key and payload, the original
/// headers, and the failure context in <c>dlq-*</c> headers. A manual replay tool reads them back.</summary>
public sealed class DeadLetterPublisher(IKafkaMessageProducer producer, TimeProvider? clock = null) : IDeadLetterPublisher
{
    private const int MaxErrorLength = 1_000;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public Task PublishAsync(ConsumedMessage message, Exception failure, int attempts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(failure);

        var headers = new Headers();
        foreach (var header in message.Headers)
        {
            if (!header.Key.StartsWith("dlq-", StringComparison.Ordinal))
            {
                headers.Add(header.Key, header.GetValueBytes());
            }
        }

        headers.Add(MessageHeaders.DlqError, Utf8(Truncate($"{failure.GetType().Name}: {failure.Message}")));
        headers.Add(MessageHeaders.DlqAttempts, Utf8(attempts.ToString(CultureInfo.InvariantCulture)));
        headers.Add(MessageHeaders.DlqSourceTopic, Utf8(message.Topic));
        headers.Add(MessageHeaders.DlqSourcePartition, Utf8(message.Partition.ToString(CultureInfo.InvariantCulture)));
        headers.Add(MessageHeaders.DlqSourceOffset, Utf8(message.Offset.ToString(CultureInfo.InvariantCulture)));
        headers.Add(MessageHeaders.DlqFailedAt, Utf8(_clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)));

        return producer.ForwardAsync(KafkaTopics.DeadLetterOf(message.Topic), message.Key, message.Value, headers, cancellationToken);
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static string Truncate(string value) => value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];
}
