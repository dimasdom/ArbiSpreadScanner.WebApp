using System.Runtime.CompilerServices;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentResults;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Infrastructure.Realtime;

public sealed class SpreadStreamService(
    ISubscriptionService subscriptions,
    ITradeOpportunityRepository spreads,
    ITradeOpportunityTickerRepository tickers,
    ISpreadStreamBroker broker,
    IOptions<SpreadStreamOptions> options) : ISpreadStreamService
{
    private enum Wait { Ready, Idle, Ended }

    public async Task<Result<IAsyncEnumerable<SpreadStreamEvent>>> OpenAsync(Guid spreadId, long? lastEventId, CancellationToken cancellationToken)
    {
        if (!await subscriptions.CheckIfUserHasActiveSubscriptionAsync())
        {
            return Result.Fail<IAsyncEnumerable<SpreadStreamEvent>>(TypedErrors.Forbidden("No active subscription"));
        }

        var spread = await spreads.GetByGuidAsync(spreadId);
        if (spread is null)
        {
            return Result.Fail<IAsyncEnumerable<SpreadStreamEvent>>(TypedErrors.NotFound("Spread not found"));
        }

        return Result.Ok(Stream(spread, lastEventId, cancellationToken));
    }

    private async IAsyncEnumerable<SpreadStreamEvent> Stream(
        TradeOpportunityModel spread,
        long? lastEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (spread.OrderStatus == OrderStatus.Closed)
        {
            // A client reconnecting after the spread closed would otherwise wait on a stream that will never get another event.
            yield return SpreadStreamEvent.Close(spread.Guid);
            yield break;
        }

        var settings = options.Value;
        var keepAlive = TimeSpan.FromSeconds(settings.KeepAliveSeconds);

        // Subscribe before replaying: anything published while the replay query runs is buffered, not lost.
        using var subscription = broker.Subscribe(spread.Guid);
        SpreadStreamMetrics.StreamOpened();
        try
        {
            var lastSent = long.MinValue;

            if (lastEventId is { } cursor)
            {
                lastSent = cursor;
                var missed = await tickers.GetSinceWithoutOrderBookAsync(
                    spread.Guid, DateTimeOffset.FromUnixTimeMilliseconds(cursor).UtcDateTime, settings.ReplayLimit);

                foreach (var ticker in missed)
                {
                    var replayed = SpreadStreamEvent.Ticker(ticker);
                    lastSent = Math.Max(lastSent, long.Parse(replayed.Id!));
                    yield return replayed;
                }
            }

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lifetime.CancelAfter(TimeSpan.FromMinutes(settings.MaxLifetimeMinutes));

            while (true)
            {
                var wait = await WaitForEventAsync(subscription, keepAlive, lifetime.Token);
                if (wait == Wait.Ended) yield break;

                if (wait == Wait.Idle)
                {
                    yield return SpreadStreamEvent.KeepAlive;
                    continue;
                }

                while (subscription.Reader.TryRead(out var next))
                {
                    if (next.Type == SpreadStreamEventTypes.Close)
                    {
                        yield return next;
                        yield break;
                    }

                    // A ticker can arrive both via the replay query and the live subscription; send each id once, in order.
                    if (next.Id is not null)
                    {
                        var id = long.Parse(next.Id);
                        if (id <= lastSent) continue;
                        lastSent = id;
                    }

                    yield return next;
                }
            }
        }
        finally
        {
            SpreadStreamMetrics.StreamClosed();
        }
    }

    // Not inlined into the iterator: C# forbids `yield` inside a try block that has a catch.
    private static async Task<Wait> WaitForEventAsync(ISpreadStreamSubscription subscription, TimeSpan keepAlive, CancellationToken lifetime)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        idle.CancelAfter(keepAlive);

        try
        {
            return await subscription.Reader.WaitToReadAsync(idle.Token) ? Wait.Ready : Wait.Ended;
        }
        catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
        {
            return Wait.Idle;
        }
        catch (OperationCanceledException)
        {
            return Wait.Ended;
        }
    }
}
