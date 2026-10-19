using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using ArbiScannerWeb.Infrastructure.Realtime;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure.Realtime;

public class SpreadStreamServiceTests
{
    private static readonly DateTime T0 = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Mock<ISubscriptionService> _subscriptions = new();
    private readonly Mock<ITradeOpportunityRepository> _spreads = new();
    private readonly Mock<ITradeOpportunityTickerRepository> _tickers = new();
    private readonly ObservableBroker _broker;
    private readonly SpreadStreamOptions _options = new() { KeepAliveSeconds = 60, MaxLifetimeMinutes = 60 };
    private readonly Guid _spreadId = Guid.NewGuid();

    public SpreadStreamServiceTests()
    {
        _broker = new ObservableBroker(_options);
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreads.Setup(r => r.GetByGuidAsync(_spreadId)).ReturnsAsync(TestSpreads.Build(_spreadId, OrderStatus.Open));
        _tickers
            .Setup(r => r.GetSinceWithoutOrderBookAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync([]);
    }

    private SpreadStreamService CreateSut() =>
        new(_subscriptions.Object, _spreads.Object, _tickers.Object, _broker, Options.Create(_options));

    private static TradeOpportunityTickerModel Ticker(int secondsAfterT0) =>
        new() { Guid = Guid.Empty, Spread = secondsAfterT0, DateTime = T0.AddSeconds(secondsAfterT0) };

    private static long IdOf(int secondsAfterT0) => new DateTimeOffset(T0.AddSeconds(secondsAfterT0)).ToUnixTimeMilliseconds();

    private async Task<IAsyncEnumerable<SpreadStreamEvent>> OpenAsync(long? lastEventId = null, CancellationToken ct = default)
    {
        var result = await CreateSut().OpenAsync(_spreadId, lastEventId, ct);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    /// <summary>Starts consuming and returns once the stream has subscribed, so a following Publish cannot race the subscribe.</summary>
    private async Task<(List<SpreadStreamEvent> Received, Task Done)> ConsumeAsync(IAsyncEnumerable<SpreadStreamEvent> stream, CancellationToken ct = default)
    {
        var received = new List<SpreadStreamEvent>();
        var done = Task.Run(async () =>
        {
            await foreach (var item in stream.WithCancellation(ct)) lock (received) received.Add(item);
        }, CancellationToken.None);

        await _broker.Subscribed.WaitAsync(Timeout);
        return (received, done);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task OpenAsync_WithoutActiveSubscription_FailsWithForbidden()
    {
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(false);

        var result = await CreateSut().OpenAsync(_spreadId, null, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Metadata[TypedErrors.HttpStatusCodeMetadataKey].Should().Be(403);
        _spreads.Verify(r => r.GetByGuidAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task OpenAsync_UnknownSpread_FailsWithNotFound()
    {
        var result = await CreateSut().OpenAsync(Guid.NewGuid(), null, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Metadata[TypedErrors.HttpStatusCodeMetadataKey].Should().Be(404);
    }

    [Fact]
    public async Task Stream_ClosedSpread_YieldsOnlyCloseWithoutSubscribing()
    {
        _spreads.Setup(r => r.GetByGuidAsync(_spreadId)).ReturnsAsync(TestSpreads.Build(_spreadId, OrderStatus.Closed));
        var stream = await OpenAsync();

        var events = await stream.ToListAsync();

        events.Should().ContainSingle().Which.Type.Should().Be(SpreadStreamEventTypes.Close);
        _broker.SubscribeCount.Should().Be(0);
    }

    [Fact]
    public async Task Stream_DeliversLiveTickersInOrder()
    {
        var (received, _) = await ConsumeAsync(await OpenAsync());

        foreach (var s in new[] { 1, 2, 3 }) _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(s)));

        await WaitUntilAsync(() => received.Count == 3);
        received.Select(e => e.Id).Should().Equal(IdOf(1).ToString(), IdOf(2).ToString(), IdOf(3).ToString());
    }

    [Fact]
    public async Task Stream_OnlyDeliversEventsOfItsOwnSpread()
    {
        var (received, _) = await ConsumeAsync(await OpenAsync());

        _broker.Publish(Guid.NewGuid(), SpreadStreamEvent.Ticker(Ticker(1)));
        _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(2)));

        await WaitUntilAsync(() => received.Count == 1);
        received.Single().Id.Should().Be(IdOf(2).ToString());
    }

    [Fact]
    public async Task Stream_WithoutCursor_DoesNotQueryTheReplay()
    {
        var (_, _) = await ConsumeAsync(await OpenAsync(lastEventId: null));

        _tickers.Verify(r => r.GetSinceWithoutOrderBookAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Stream_WithCursor_ReplaysMissedTickersFirstThenGoesLive()
    {
        _tickers
            .Setup(r => r.GetSinceWithoutOrderBookAsync(_spreadId, T0.AddSeconds(1), _options.ReplayLimit))
            .ReturnsAsync([Ticker(2), Ticker(3)]);

        var (received, _) = await ConsumeAsync(await OpenAsync(lastEventId: IdOf(1)));
        _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(4)));

        await WaitUntilAsync(() => received.Count == 3);
        received.Select(e => e.Id).Should().Equal(IdOf(2).ToString(), IdOf(3).ToString(), IdOf(4).ToString());
    }

    [Fact]
    public async Task Stream_DropsLiveEventsAlreadyCoveredByTheReplayOrTheCursor()
    {
        _tickers
            .Setup(r => r.GetSinceWithoutOrderBookAsync(_spreadId, It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync([Ticker(2), Ticker(3)]);

        var (received, _) = await ConsumeAsync(await OpenAsync(lastEventId: IdOf(1)));
        _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(1)));  // the cursor itself
        _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(3)));  // already replayed
        _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(4)));

        await WaitUntilAsync(() => received.Count == 3);
        received.Select(e => e.Id).Should().Equal(IdOf(2).ToString(), IdOf(3).ToString(), IdOf(4).ToString());
    }

    [Fact]
    public async Task Stream_SubscribesBeforeReplaying_SoEventsPublishedDuringTheReplayAreNotLost()
    {
        _tickers
            .Setup(r => r.GetSinceWithoutOrderBookAsync(_spreadId, It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns(() =>
            {
                // Published while the replay query is still running.
                _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(5)));
                return Task.FromResult(new List<TradeOpportunityTickerModel> { Ticker(2) });
            });

        var stream = await OpenAsync(lastEventId: IdOf(1));
        var events = await stream.Take(2).ToListAsync();

        events.Select(e => e.Id).Should().Equal(IdOf(2).ToString(), IdOf(5).ToString());
    }

    [Fact]
    public async Task Stream_PassesTheCursorAsUtcAndTheConfiguredReplayLimit()
    {
        _options.ReplayLimit = 42;
        DateTime? since = null;
        _tickers
            .Setup(r => r.GetSinceWithoutOrderBookAsync(_spreadId, It.IsAny<DateTime>(), 42))
            .Callback<Guid, DateTime, int>((_, s, _) => since = s)
            .ReturnsAsync([]);

        await ConsumeAsync(await OpenAsync(lastEventId: IdOf(7)));

        since.Should().Be(T0.AddSeconds(7));
        since!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Stream_CloseEvent_IsDeliveredAndEndsTheStream()
    {
        var (received, done) = await ConsumeAsync(await OpenAsync());

        _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(1)));
        _broker.Publish(_spreadId, SpreadStreamEvent.Close(_spreadId));
        _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(2)));

        await done.WaitAsync(Timeout);
        received.Select(e => e.Type).Should().Equal(SpreadStreamEventTypes.Ticker, SpreadStreamEventTypes.Close);
    }

    [Fact]
    public async Task Stream_WhenIdle_EmitsKeepAlives()
    {
        _options.KeepAliveSeconds = 0.03;
        var (received, _) = await ConsumeAsync(await OpenAsync());

        await WaitUntilAsync(() => received.Count >= 2);

        received.Should().OnlyContain(e => e.Type == SpreadStreamEventTypes.KeepAlive);
    }

    [Fact]
    public async Task Stream_EndsAfterTheMaxLifetime_SoTheClientReconnectsWithAFreshToken()
    {
        _options.MaxLifetimeMinutes = 0.001; // 60 ms
        var (_, done) = await ConsumeAsync(await OpenAsync());

        await done.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Stream_WhenTheClientDisconnects_EndsWithoutThrowing()
    {
        using var cts = new CancellationTokenSource();
        var (_, done) = await ConsumeAsync(await OpenAsync(ct: cts.Token), cts.Token);

        await cts.CancelAsync();

        await done.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Stream_Ending_ReleasesTheSubscription()
    {
        var (_, done) = await ConsumeAsync(await OpenAsync());

        _broker.Publish(_spreadId, SpreadStreamEvent.Close(_spreadId));
        await done.WaitAsync(Timeout);

        _broker.DisposedCount.Should().Be(1);
    }

    [Fact]
    public async Task Stream_AbandonedByTheConsumer_ReleasesTheSubscription()
    {
        var stream = await OpenAsync();

        await using (var enumerator = stream.GetAsyncEnumerator())
        {
            var move = enumerator.MoveNextAsync().AsTask();
            await _broker.Subscribed.WaitAsync(Timeout);
            _broker.Publish(_spreadId, SpreadStreamEvent.Ticker(Ticker(1)));
            (await move.WaitAsync(Timeout)).Should().BeTrue();
        } // disposed after a single event, as when the HTTP connection drops mid-write

        _broker.DisposedCount.Should().Be(1);
    }

    /// <summary>Real broker plus the hooks the tests need: a signal for "stream subscribed" and subscribe/dispose counters.</summary>
    private sealed class ObservableBroker(SpreadStreamOptions options) : ISpreadStreamBroker
    {
        private readonly SpreadStreamBroker _inner = new(Options.Create(options));
        private readonly TaskCompletionSource _subscribed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _subscribeCount;
        private int _disposedCount;

        public Task Subscribed => _subscribed.Task;
        public int SubscribeCount => _subscribeCount;
        public int DisposedCount => _disposedCount;

        public ISpreadStreamSubscription Subscribe(Guid spreadId)
        {
            var inner = _inner.Subscribe(spreadId);
            Interlocked.Increment(ref _subscribeCount);
            _subscribed.TrySetResult();
            return new Tracked(inner, () => Interlocked.Increment(ref _disposedCount));
        }

        public void Publish(Guid spreadId, SpreadStreamEvent streamEvent) => _inner.Publish(spreadId, streamEvent);

        private sealed class Tracked(ISpreadStreamSubscription inner, Action onDispose) : ISpreadStreamSubscription
        {
            public System.Threading.Channels.ChannelReader<SpreadStreamEvent> Reader => inner.Reader;

            public void Dispose()
            {
                inner.Dispose();
                onDispose();
            }
        }
    }
}
