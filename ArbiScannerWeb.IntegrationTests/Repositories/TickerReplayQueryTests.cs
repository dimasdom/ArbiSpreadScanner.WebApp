using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ArbiScannerWeb.IntegrationTests.Repositories;

/// <summary>Runs against a real MongoDB (Testcontainers): the replay query depends on the driver's sort, projection and
/// millisecond date handling, which a mock cannot prove.</summary>
[Collection(WebApiCollection.Name)]
public class TickerReplayQueryTests(WebApiTestFixture fixture)
{
    private static readonly DateTime Origin = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private ITradeOpportunityTickerRepository Repository =>
        fixture.Factory.Services.GetRequiredService<ITradeOpportunityTickerRepository>();

    private async Task SeedAsync(Guid guid, params int[] secondsAfterOrigin)
    {
        foreach (var seconds in secondsAfterOrigin)
        {
            await Repository.InsertAsync(new TradeOpportunityTickerModel
            {
                Guid = guid,
                Symbol = "BTC/USDT",
                Spread = seconds,
                DateTime = Origin.AddSeconds(seconds),
                BidsExchangeA = [new OrderLevelModel { Price = 1, Amount = 1 }],
                AsksExchangeA = [new OrderLevelModel { Price = 2, Amount = 1 }],
                BidsExchangeB = [new OrderLevelModel { Price = 3, Amount = 1 }],
                AsksExchangeB = [new OrderLevelModel { Price = 4, Amount = 1 }],
            });
        }
    }

    [Fact]
    public async Task ReturnsTickersStrictlyAfterTheCursor_OldestFirst()
    {
        var guid = Guid.NewGuid();
        await SeedAsync(guid, 1, 2, 3, 4);

        var result = await Repository.GetSinceWithoutOrderBookAsync(guid, Origin.AddSeconds(2), limit: 100);

        result.Select(t => t.Spread).Should().Equal(3, 4);
    }

    [Fact]
    public async Task CursorBeforeEverything_ReturnsAllInChronologicalOrder()
    {
        var guid = Guid.NewGuid();
        await SeedAsync(guid, 3, 1, 2);

        var result = await Repository.GetSinceWithoutOrderBookAsync(guid, Origin, limit: 100);

        result.Select(t => t.Spread).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task CursorAtOrAfterTheNewest_ReturnsNothing()
    {
        var guid = Guid.NewGuid();
        await SeedAsync(guid, 1, 2);

        (await Repository.GetSinceWithoutOrderBookAsync(guid, Origin.AddSeconds(2), limit: 100)).Should().BeEmpty();
        (await Repository.GetSinceWithoutOrderBookAsync(guid, Origin.AddSeconds(60), limit: 100)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenMoreThanLimitQualify_KeepsTheNewestAndStaysChronological()
    {
        var guid = Guid.NewGuid();
        await SeedAsync(guid, 1, 2, 3, 4, 5);

        var result = await Repository.GetSinceWithoutOrderBookAsync(guid, Origin, limit: 2);

        result.Select(t => t.Spread).Should().Equal(4, 5);
    }

    [Fact]
    public async Task OnlyReturnsTheRequestedSpread()
    {
        var guid = Guid.NewGuid();
        await SeedAsync(guid, 1);
        await SeedAsync(Guid.NewGuid(), 2, 3);

        var result = await Repository.GetSinceWithoutOrderBookAsync(guid, Origin, limit: 100);

        result.Should().ContainSingle().Which.Guid.Should().Be(guid);
    }

    [Fact]
    public async Task OmitsOrderBooksButKeepsTheChartFields()
    {
        var guid = Guid.NewGuid();
        await SeedAsync(guid, 7);

        var ticker = (await Repository.GetSinceWithoutOrderBookAsync(guid, Origin, limit: 100)).Single();

        ticker.BidsExchangeA.Should().BeNullOrEmpty();
        ticker.AsksExchangeA.Should().BeNullOrEmpty();
        ticker.BidsExchangeB.Should().BeNullOrEmpty();
        ticker.AsksExchangeB.Should().BeNullOrEmpty();
        ticker.Spread.Should().Be(7);
        ticker.Symbol.Should().Be("BTC/USDT");
        ticker.DateTime.Should().BeCloseTo(Origin.AddSeconds(7), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task UnknownSpread_ReturnsEmpty()
    {
        (await Repository.GetSinceWithoutOrderBookAsync(Guid.NewGuid(), Origin, limit: 100)).Should().BeEmpty();
    }
}
