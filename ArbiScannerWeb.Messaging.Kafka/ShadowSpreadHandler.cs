using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Messaging.Kafka;

/// <summary>
/// Shadow-mode handler: proves the Kafka path carries the same events without doing anything user-visible. It decodes the
/// payload (so a contract break shows up as a dead letter), checks the partition key against the one derived from the payload,
/// records the message for the parity check and logs. It writes no database rows, sends no notifications and pushes nothing.
/// </summary>
public sealed class ShadowSpreadHandler(
    IParityRecorder parity,
    IOptions<MessagingOptions> options,
    ILogger<ShadowSpreadHandler> logger) : IKafkaMessageHandler
{
    public async Task HandleAsync(ConsumedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        Domain.Models.TradeOpportunityModel model;
        try
        {
            model = TradeOpportunitySerializer.Deserialize(message.Value);
        }
        catch (Exception ex)
        {
            throw new NonRetryableMessageException("Payload is not a decodable TradeOpportunityModel.", ex);
        }

        var expectedKey = SpreadEventKeys.For(model);
        if (!string.Equals(expectedKey, message.Key, StringComparison.Ordinal))
        {
            ParityMetrics.KeyMismatches.Add(1, new KeyValuePair<string, object?>("service", options.Value.ServiceName));
            logger.LogWarning("Kafka key {ActualKey} differs from the key derived from the payload {ExpectedKey}", message.Key, expectedKey);
        }

        var messageId = message.Metadata.MessageId;
        if (string.IsNullOrEmpty(messageId))
        {
            logger.LogWarning("Kafka message has no message-id header; it cannot be matched against RabbitMQ");
            return;
        }

        await parity.RecordAsync(MessagingBackends.Kafka, message.Key, messageId, new DateTimeOffset(message.TimestampUtc, TimeSpan.Zero));

        logger.LogInformation(
            "Shadow: would apply {ActionType} for spread {Guid} ({Symbol}, spread {Spread})",
            model.ActionType,
            model.Guid,
            model.Symbol,
            model.Spread);
    }
}
