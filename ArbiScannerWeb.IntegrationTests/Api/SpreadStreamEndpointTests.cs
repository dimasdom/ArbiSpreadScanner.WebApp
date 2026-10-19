using System.Net;
using System.Text.Json;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ArbiScannerWeb.IntegrationTests.Api;

/// <summary>The whole stack for real: JWT auth, JIT user provisioning, the stream endpoint, Kafka-handler-equivalent service calls
/// (AddSpread / UpdateSpread / CloseSpread), and MongoDB for the replay.</summary>
[Collection(WebApiCollection.Name)]
public class SpreadStreamEndpointTests(WebApiTestFixture fixture)
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task WithoutAuth_ReturnsUnauthorized()
    {
        var response = await fixture.Factory.CreateClient().GetAsync($"/api/TradeOpportunity/Stream/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownSpread_ReturnsNotFound()
    {
        var response = await fixture.Factory.CreateAuthenticatedClient().GetAsync($"/api/TradeOpportunity/Stream/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LiveUpdates_ArriveAsTickerEvents_AndTheCloseEndsTheStream()
    {
        var spread = BuildSpread();
        await WithService(s => s.AddSpread(spread));

        var client = fixture.Factory.CreateAuthenticatedClient();
        using var response = await client.GetAsync($"/api/TradeOpportunity/Stream/{spread.Guid}", HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        using var timeout = new CancellationTokenSource(ReadTimeout);
        (await reader.ReadLineAsync(timeout.Token)).Should().StartWith(": connected");

        spread.ActionType = MarketPositionAction.Update;
        spread.Spread = 2.5;
        await WithService(s => s.UpdateSpread(spread));

        var ticker = await ReadEventAsync(reader, timeout.Token);
        ticker.Event.Should().Be("ticker");
        ticker.Id.Should().NotBeNullOrEmpty();
        JsonDocument.Parse(ticker.Data!).RootElement.GetProperty("spread").GetDouble().Should().Be(2.5);

        spread.ActionType = MarketPositionAction.Close;
        await WithService(s => s.CloseSpread(spread));

        (await ReadEventAsync(reader, timeout.Token)).Event.Should().Be("close");
        (await reader.ReadLineAsync(timeout.Token)).Should().BeNull("the server ends the stream after the close event");
    }

    [Fact]
    public async Task LastEventId_ReplaysTickersStoredWhileTheClientWasAway()
    {
        var spread = BuildSpread();
        await WithService(s => s.AddSpread(spread));
        spread.ActionType = MarketPositionAction.Update;
        spread.Spread = 3.5;
        await WithService(s => s.UpdateSpread(spread));

        var client = fixture.Factory.CreateAuthenticatedClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/TradeOpportunity/Stream/{spread.Guid}");
        request.Headers.Add("Last-Event-ID", "0"); // "I have nothing": everything stored is newer
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        using var timeout = new CancellationTokenSource(ReadTimeout);
        (await reader.ReadLineAsync(timeout.Token)).Should().StartWith(": connected");

        // Skip the preamble's remaining lines, then the first replayed ticker (Open + Update tickers were stored).
        var replayed = await ReadEventAsync(reader, timeout.Token);
        replayed.Event.Should().Be("ticker");
        var data = JsonDocument.Parse(replayed.Data!).RootElement;
        data.GetProperty("symbol").GetString().Should().Be(spread.Symbol);
        data.TryGetProperty("bidsExchangeA", out var book).Should().BeTrue();
        (book.ValueKind is JsonValueKind.Null || book.GetArrayLength() == 0).Should().BeTrue("replayed tickers carry no order book");
    }

    private async Task WithService(Func<ITradeOpportunityService, Task<FluentResults.Result>> action)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        (await action(scope.ServiceProvider.GetRequiredService<ITradeOpportunityService>())).IsSuccess.Should().BeTrue();
    }

    private static async Task<(string? Id, string? Event, string? Data)> ReadEventAsync(StreamReader reader, CancellationToken ct)
    {
        string? id = null, ev = null, data = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0)
            {
                if (ev is not null) return (id, ev, data);
                continue; // end of the preamble / a keep-alive comment block
            }

            if (line.StartsWith("id:", StringComparison.Ordinal)) id = line[3..].Trim();
            else if (line.StartsWith("event:", StringComparison.Ordinal)) ev = line[6..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal)) data = line[5..].Trim();
        }

        return (id, ev, data);
    }

    private static TradeOpportunityModel BuildSpread()
    {
        ExchangeRateModel Rate(string exchange) => new()
        {
            Symbol = "BTC/USDT",
            Exchange = exchange,
            ExchangeRate = 50_000,
            VolumeAsk = 100,
            VolumeBid = 100,
        };

        return new TradeOpportunityModel
        {
            Guid = Guid.NewGuid(),
            Symbol = "BTC/USDT",
            Type = SpreadType.Spot,
            ActionType = MarketPositionAction.Open,
            ExchangeRateA = Rate("Binance"),
            ExchangeRateB = Rate("Bybit"),
            ExchangeShort = Rate("Binance"),
            ExchangeLong = Rate("Bybit"),
            Spread = 1.5,
            StartSpread = 1.5,
            SummaryTarrif = 0.1,
            PossibleProfit = 10,
            BidsExchangeA = [new OrderLevelModel { Price = 1, Amount = 1 }],
        };
    }
}
