using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Realtime;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure.Realtime;

public class SseNotifierTests
{
    private readonly Mock<ISpreadStreamBroker> _broker = new();
    private readonly List<(Guid Spread, SpreadStreamEvent Event)> _published = [];
    private readonly SseNotifier _sut;
    private readonly Guid _spread = Guid.NewGuid();

    public SseNotifierTests()
    {
        _broker.Setup(b => b.Publish(It.IsAny<Guid>(), It.IsAny<SpreadStreamEvent>()))
            .Callback<Guid, SpreadStreamEvent>((g, e) => _published.Add((g, e)));
        _sut = new SseNotifier(_broker.Object, NullLogger<SseNotifier>.Instance);
    }

    private string Group => _spread.ToString().ToLowerInvariant();

    [Fact]
    public async Task Ticker_IsPublishedAsATickerEventForThatSpread()
    {
        var ticker = new TradeOpportunityTickerModel { DateTime = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc), Spread = 1.5 };

        await _sut.NotifyGroupAsync(Group, new MessageDto { Ticker = ticker });

        var (spread, evt) = _published.Should().ContainSingle().Subject;
        spread.Should().Be(_spread);
        evt.Type.Should().Be(SpreadStreamEventTypes.Ticker);
        evt.Data.Should().BeSameAs(ticker);
        evt.Id.Should().Be(SpreadStreamEvent.ToEventId(ticker.DateTime));
    }

    [Fact]
    public async Task ClosingTradeOpportunity_IsPublishedAsACloseEvent()
    {
        await _sut.NotifyGroupAsync(Group, new MessageDto { TradeOpportunity = TestSpreads.Build(action: MarketPositionAction.Close) });

        _published.Should().ContainSingle().Which.Event.Type.Should().Be(SpreadStreamEventTypes.Close);
    }

    [Theory]
    [InlineData(MarketPositionAction.Open)]
    [InlineData(MarketPositionAction.Update)]
    public async Task NonClosingTradeOpportunity_IsNotPublished(MarketPositionAction action)
    {
        await _sut.NotifyGroupAsync(Group, new MessageDto { TradeOpportunity = TestSpreads.Build(action: action) });

        _published.Should().BeEmpty();
    }

    [Fact]
    public async Task NonGuidGroup_IsIgnored()
    {
        await _sut.NotifyGroupAsync("not-a-guid", new MessageDto { Ticker = new TradeOpportunityTickerModel() });

        _published.Should().BeEmpty();
    }

    [Fact]
    public async Task UnsupportedPayload_IsIgnored()
    {
        await _sut.NotifyGroupAsync(Group, new { message = "hi" });

        _published.Should().BeEmpty();
    }

    [Fact]
    public async Task UppercaseGuidGroup_StillReachesTheSpread()
    {
        await _sut.NotifyGroupAsync(Group.ToUpperInvariant(), new MessageDto { Ticker = new TradeOpportunityTickerModel() });

        _published.Should().ContainSingle().Which.Spread.Should().Be(_spread);
    }
}
