using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Messaging.Kafka;

/// <summary>
/// The live Kafka consumer of the web API: applies each spread event (Open / Update / Close) to Mongo and pushes it to the open SSE streams
/// through <see cref="ITradeOpportunityService"/>. Registered scoped, so every message gets its own DI scope and DbContext.
/// </summary>
/// <remarks>
/// A failed <see cref="Result"/> is turned into an exception so the message is retried and then dead-lettered. (The old RabbitMQ
/// path ignored the result and acknowledged the message, silently losing it.) Duplicates, whether from a redelivery after a crash
/// or a rebalance, are suppressed by message-id; the one residual window is a crash between the ticker insert and the done marker,
/// which can add one extra chart point once the 60 s claim expires.
/// </remarks>
public sealed class TradeOpportunityKafkaHandler(
    ITradeOpportunityService spreads,
    IIdempotencyStore idempotency,
    IOptions<MessagingOptions> options,
    ILogger<TradeOpportunityKafkaHandler> logger) : IKafkaMessageHandler
{
    private static readonly TimeSpan ClaimWait = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ClaimPoll = TimeSpan.FromSeconds(1);

    public async Task HandleAsync(ConsumedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var model = Decode(message);
        var messageId = message.Metadata.MessageId
            ?? throw new NonRetryableMessageException("The message-id header is required to process a spread event idempotently.");

        if (model.ActionType is not (MarketPositionAction.Open or MarketPositionAction.Update or MarketPositionAction.Close))
        {
            throw new NonRetryableMessageException($"Unknown action type {model.ActionType}.");
        }

        var ran = await IdempotentExecutor.RunOnceAsync(
            idempotency,
            $"spread-dedupe:{options.Value.ServiceName}:{messageId}",
            _ => ApplyAsync(model),
            ClaimWait,
            ClaimPoll,
            cancellationToken);

        if (!ran)
        {
            ConsumerMetrics.DuplicatesSkipped.Add(1, new KeyValuePair<string, object?>("service", options.Value.ServiceName));
            logger.LogInformation("Duplicate delivery skipped: message-id {MessageId}", messageId);
            return;
        }

        logger.LogInformation("Applied {ActionType} for spread {Guid}", model.ActionType, model.Guid);
    }

    private async Task ApplyAsync(TradeOpportunityModel model)
    {
        var result = model.ActionType switch
        {
            MarketPositionAction.Open => await spreads.AddSpread(model),
            MarketPositionAction.Update => await spreads.UpdateSpread(model),
            _ => await spreads.CloseSpread(model),
        };

        if (result.IsFailed)
        {
            throw new InvalidOperationException(
                $"{model.ActionType} for spread {model.Guid} failed: {string.Join("; ", result.Errors.Select(e => e.Message))}");
        }
    }

    private static TradeOpportunityModel Decode(ConsumedMessage message)
    {
        try
        {
            return TradeOpportunitySerializer.Deserialize(message.Value);
        }
        catch (Exception ex)
        {
            throw new NonRetryableMessageException("Payload is not a decodable TradeOpportunityModel.", ex);
        }
    }
}
