using System.Net.Http.Json;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ArbiScannerWeb.IntegrationTests.Api;

[Collection(WebApiCollection.Name)]
public class SpreadStatsControllerTests(WebApiTestFixture fixture)
{
    [Fact]
    public async Task GetLatest_WithoutAuth_ReturnsGeneratedSnapshot()
    {
        var snapshot = await GenerateSnapshotAsync();
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/SpreadStats/GetLatest");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<SpreadStatsSnapshotModel>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeTrue();
        result.Value!.TotalSpreadsAnalyzed.Should().Be(snapshot.TotalSpreadsAnalyzed);
    }

    [Fact]
    public async Task GetById_ExistingSnapshot_ReturnsIt()
    {
        var snapshot = await GenerateSnapshotAsync();
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/api/SpreadStats/GetById?id={snapshot.Id}");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<SpreadStatsSnapshotModel>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(snapshot.Id);
    }

    [Fact]
    public async Task GetById_UnknownGuid_ReturnsFailure()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/api/SpreadStats/GetById?id={Guid.NewGuid()}");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<SpreadStatsSnapshotModel>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetSnapshotIndex_AfterGenerating_ContainsTheNewSnapshot()
    {
        var snapshot = await GenerateSnapshotAsync();
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/SpreadStats/GetSnapshotIndex");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<List<SnapshotIndexEntry>>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Id == snapshot.Id);
    }

    // Regression guard for the deliberate "Stats are public" decision — no Authorization
    // header is set here, unlike every TradeOpportunityController test.
    [Fact]
    public async Task GetSnapshotIndex_WithoutAuth_DoesNotReturnUnauthorized()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/SpreadStats/GetSnapshotIndex");

        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSnapshotIndex_ExcludesSnapshotsOlderThan30Days()
    {
        var recent = await GenerateSnapshotAsync();
        var old = BuildEmptySnapshot(DateTime.UtcNow.AddDays(-31));

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var statsRepository = scope.ServiceProvider.GetRequiredService<ISpreadStatsRepository>();
            await statsRepository.InsertSnapshotAsync(old);
        }

        var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync("/api/SpreadStats/GetSnapshotIndex");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<List<SnapshotIndexEntry>>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Id == recent.Id);
        result.Value.Should().NotContain(e => e.Id == old.Id);
    }

    private static SpreadStatsSnapshotModel BuildEmptySnapshot(DateTime generatedAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        GeneratedAtUtc = generatedAtUtc,
        TotalSpreadsAnalyzed = 0,
        TopSymbolsByAverageSpread = [],
        TopExchangesByCount = [],
        TopExchangePairsByCount = [],
        MedianVolumeByExchange = [],
        SpreadTypeDistribution = [],
        TopSymbolsByCount = [],
    };

    private async Task<SpreadStatsSnapshotModel> GenerateSnapshotAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();

        var tradeOpportunityService = scope.ServiceProvider.GetRequiredService<ITradeOpportunityService>();
        (await tradeOpportunityService.AddSpread(BuildSpread())).IsSuccess.Should().BeTrue();

        var statsService = scope.ServiceProvider.GetRequiredService<ISpreadStatsService>();
        var result = await statsService.GenerateAndPersistSnapshotAsync();
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static TradeOpportunityModel BuildSpread()
    {
        ExchangeRateModel Rate(string exchange) => new()
        {
            Symbol = "BTC/USDT",
            Exchange = exchange,
            ExchangeRate = 50_000,
            VolumeAsk = 100,
            VolumeBid = 100
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
        };
    }
}
