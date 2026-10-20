using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Realtime;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Tests.Infrastructure.Realtime;

public class SpreadStreamBrokerTests
{
    private static SpreadStreamBroker CreateSut(int bufferSize = 8) =>
        new(Options.Create(new SpreadStreamOptions { SubscriberBufferSize = bufferSize }));

    private static SpreadStreamEvent Event(string id) => new(SpreadStreamEventTypes.Ticker, id);

    private static List<string?> Drain(ISpreadStreamSubscription subscription)
    {
        var ids = new List<string?>();
        while (subscription.Reader.TryRead(out var item)) ids.Add(item.Id);
        return ids;
    }

    [Fact]
    public void Publish_DeliversToEverySubscriberOfThatSpread()
    {
        var sut = CreateSut();
        var spread = Guid.NewGuid();
        using var first = sut.Subscribe(spread);
        using var second = sut.Subscribe(spread);

        sut.Publish(spread, Event("1"));

        Drain(first).Should().Equal("1");
        Drain(second).Should().Equal("1");
    }

    [Fact]
    public void Publish_DoesNotLeakAcrossSpreads()
    {
        var sut = CreateSut();
        using var other = sut.Subscribe(Guid.NewGuid());

        sut.Publish(Guid.NewGuid(), Event("1"));

        Drain(other).Should().BeEmpty();
    }

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Publish(Guid.NewGuid(), Event("1"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Publish_PreservesOrder()
    {
        var sut = CreateSut();
        var spread = Guid.NewGuid();
        using var subscription = sut.Subscribe(spread);

        foreach (var id in new[] { "1", "2", "3" }) sut.Publish(spread, Event(id));

        Drain(subscription).Should().Equal("1", "2", "3");
    }

    [Fact]
    public void Publish_WhenBufferIsFull_DropsOldestAndKeepsNewest()
    {
        var sut = CreateSut(bufferSize: 2);
        var spread = Guid.NewGuid();
        using var slow = sut.Subscribe(spread);

        foreach (var id in new[] { "1", "2", "3", "4" }) sut.Publish(spread, Event(id));

        Drain(slow).Should().Equal("3", "4");
    }

    [Fact]
    public void Publish_SlowSubscriberDoesNotStarveFastOne()
    {
        var sut = CreateSut(bufferSize: 2);
        var spread = Guid.NewGuid();
        using var slow = sut.Subscribe(spread);
        using var fast = sut.Subscribe(spread);

        foreach (var id in new[] { "1", "2", "3" })
        {
            sut.Publish(spread, Event(id));
            Drain(fast).Should().Equal(id);
        }

        Drain(slow).Should().Equal("2", "3");
    }

    [Fact]
    public void Dispose_StopsDeliveryAndCompletesTheReader()
    {
        var sut = CreateSut();
        var spread = Guid.NewGuid();
        var subscription = sut.Subscribe(spread);

        subscription.Dispose();
        sut.Publish(spread, Event("1"));

        subscription.Reader.Completion.IsCompleted.Should().BeTrue();
        Drain(subscription).Should().BeEmpty();
    }

    [Fact]
    public void Dispose_OnlyRemovesThatSubscription()
    {
        var sut = CreateSut();
        var spread = Guid.NewGuid();
        var gone = sut.Subscribe(spread);
        using var stays = sut.Subscribe(spread);

        gone.Dispose();
        sut.Publish(spread, Event("1"));

        Drain(stays).Should().Equal("1");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var sut = CreateSut();
        var subscription = sut.Subscribe(Guid.NewGuid());

        var act = () =>
        {
            subscription.Dispose();
            subscription.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Reader_WakesUpWhenAnEventIsPublished()
    {
        var sut = CreateSut();
        var spread = Guid.NewGuid();
        using var subscription = sut.Subscribe(spread);

        var read = subscription.Reader.ReadAsync().AsTask();
        sut.Publish(spread, Event("1"));

        (await read.WaitAsync(TimeSpan.FromSeconds(5))).Id.Should().Be("1");
    }

    [Fact]
    public async Task ConcurrentSubscribePublishAndDispose_NeverThrowsOrLosesEventsForLiveSubscribers()
    {
        var sut = CreateSut(bufferSize: 10_000);
        var spread = Guid.NewGuid();
        using var steady = sut.Subscribe(spread);

        var churn = Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++) sut.Subscribe(spread).Dispose();
        })));
        var publisher = Task.Run(() =>
        {
            for (var i = 0; i < 2_000; i++) sut.Publish(spread, Event(i.ToString()));
        });

        await Task.WhenAll(churn, publisher);

        Drain(steady).Should().HaveCount(2_000);
    }
}

public class SpreadStreamEventTests
{
    [Fact]
    public void Ticker_UsesUnixMillisecondsOfTheTickerTimeAsTheEventId()
    {
        var at = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

        var evt = SpreadStreamEvent.Ticker(new TradeOpportunityTickerModel { DateTime = at });

        evt.Type.Should().Be(SpreadStreamEventTypes.Ticker);
        evt.Id.Should().Be(new DateTimeOffset(at).ToUnixTimeMilliseconds().ToString());
    }

    [Fact]
    public void Ticker_TreatsAnUnspecifiedKindAsUtc()
    {
        // Mongo returns DateTimes with Kind=Unspecified/Local depending on the driver settings; the id must not shift by the
        // server's UTC offset.
        var unspecified = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Unspecified);
        var utc = DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);

        SpreadStreamEvent.Ticker(new TradeOpportunityTickerModel { DateTime = unspecified }).Id
            .Should().Be(SpreadStreamEvent.Ticker(new TradeOpportunityTickerModel { DateTime = utc }).Id);
    }

    [Fact]
    public void Ticker_TreatsALocalKindAsItsRealInstant()
    {
        var local = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Local);

        SpreadStreamEvent.Ticker(new TradeOpportunityTickerModel { DateTime = local }).Id
            .Should().Be(new DateTimeOffset(local).ToUnixTimeMilliseconds().ToString());
    }

    [Fact]
    public void Close_CarriesTheSpreadIdAndNoEventId()
    {
        var spread = Guid.NewGuid();

        var evt = SpreadStreamEvent.Close(spread);

        evt.Type.Should().Be(SpreadStreamEventTypes.Close);
        evt.Id.Should().BeNull();
        evt.Data!.ToString().Should().Contain(spread.ToString());
    }
}
