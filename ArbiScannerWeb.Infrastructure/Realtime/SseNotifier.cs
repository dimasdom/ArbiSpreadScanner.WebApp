using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace ArbiScannerWeb.Infrastructure.Realtime;

/// <summary>Bridges <see cref="IRealtimeNotifier"/> (what <c>TradeOpportunityService</c> calls) to the SSE broker. The "group" is
/// the lowercase spread GUID, the same value SignalR used.</summary>
public sealed class SseNotifier(ISpreadStreamBroker broker, ILogger<SseNotifier> logger) : IRealtimeNotifier
{
    public Task NotifyGroupAsync(string group, object payload)
    {
        if (!Guid.TryParse(group, out var spreadId))
        {
            logger.LogWarning("Ignoring realtime notification for non-spread group {GroupName}", group);
            return Task.CompletedTask;
        }

        if (payload is not MessageDto message)
        {
            logger.LogWarning("Ignoring realtime notification with unsupported payload {PayloadType}", payload?.GetType().Name);
            return Task.CompletedTask;
        }

        if (message.Ticker is not null)
        {
            broker.Publish(spreadId, SpreadStreamEvent.Ticker(message.Ticker));
        }

        // Open/Update notifications carry nothing a stream needs (the page loads the spread over REST); only the close matters.
        if (message.TradeOpportunity is { ActionType: MarketPositionAction.Close })
        {
            broker.Publish(spreadId, SpreadStreamEvent.Close(spreadId));
        }

        return Task.CompletedTask;
    }
}
