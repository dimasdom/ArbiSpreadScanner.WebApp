using System.Text.Json;
using ArbiScannerWeb.API.Streaming;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using FluentAssertions;

namespace ArbiScannerWeb.Tests.API;

public class SseFormatterTests
{
    [Fact]
    public void Preamble_IsACommentPlusTheReconnectDelayInMilliseconds()
    {
        SseFormatter.Preamble(TimeSpan.FromSeconds(3)).Should().Be(": connected\nretry: 3000\n\n");
    }

    [Fact]
    public void KeepAlive_IsACommentSoItIsNeverDispatchedToTheClientHandlers()
    {
        SseFormatter.Format(SpreadStreamEvent.KeepAlive).Should().Be(": keep-alive\n\n");
    }

    [Fact]
    public void Ticker_WritesIdEventAndOneLineOfCamelCaseJson()
    {
        var ticker = new TradeOpportunityTickerModel
        {
            Symbol = "BTC/USDT",
            Spread = 1.5,
            EchangeA = "Binance",
            DateTime = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc),
            BidsExchangeA = [new OrderLevelModel { Price = 10, Amount = 2 }],
        };

        var text = SseFormatter.Format(SpreadStreamEvent.Ticker(ticker));

        var lines = text.Split('\n');
        lines[0].Should().Be($"id: {SpreadStreamEvent.ToEventId(ticker.DateTime)}");
        lines[1].Should().Be("event: ticker");
        lines[2].Should().StartWith("data: {");
        lines[3].Should().BeEmpty();
        lines[4].Should().BeEmpty();
        text.Should().EndWith("\n\n");

        using var json = JsonDocument.Parse(lines[2]["data: ".Length..]);
        json.RootElement.GetProperty("symbol").GetString().Should().Be("BTC/USDT");
        json.RootElement.GetProperty("spread").GetDouble().Should().Be(1.5);
        json.RootElement.GetProperty("echangeA").GetString().Should().Be("Binance"); // the SPA type keeps this spelling
        json.RootElement.GetProperty("bidsExchangeA")[0].GetProperty("price").GetDouble().Should().Be(10);
    }

    [Fact]
    public void Close_HasNoIdSoItDoesNotMoveTheResumeCursor()
    {
        var spread = Guid.NewGuid();

        var text = SseFormatter.Format(SpreadStreamEvent.Close(spread));

        text.Should().Be($"event: close\ndata: {{\"spreadId\":\"{spread}\"}}\n\n");
    }

    [Fact]
    public void DataContainingNewlinesAndQuotes_StaysOnOneLine()
    {
        var text = SseFormatter.Format(new SpreadStreamEvent("custom", "1", new { note = "line1\nline2 \"quoted\"" }));

        text.Split('\n').Count(l => l.StartsWith("data:", StringComparison.Ordinal)).Should().Be(1);
        text.Should().Contain("line1\\nline2");
    }

    [Fact]
    public void NullEvent_Throws()
    {
        var act = () => SseFormatter.Format(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
