using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class SpreadStatsServiceTests
{
    private readonly Mock<ITradeOpportunityRepository> _spreadRepo = new();
    private readonly Mock<ISpreadStatsRepository> _statsRepo = new();
    private readonly Mock<IConnectionMultiplexer> _redis;
    private readonly Mock<IDatabase> _redisDb;
    private readonly SpreadStatsService _sut;

    public SpreadStatsServiceTests()
    {
        (_redis, _redisDb) = MockHelpers.CreateRedisMocks();
        _redisDb.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _sut = new SpreadStatsService(
            _spreadRepo.Object,
            _statsRepo.Object,
            _redis.Object,
            NullLogger<SpreadStatsService>.Instance);
    }

    private static ExchangeRateModel MakeLeg(string exchange, double volumeAsk = 100, double volumeBid = 100) => new()
    {
        Symbol = "BTC/USDT",
        Exchange = exchange,
        VolumeAsk = volumeAsk,
        VolumeBid = volumeBid,
    };

    private static TradeOpportunityModel MakeSpread(string symbol, string exchangeA, string exchangeB, double spread, SpreadType type = SpreadType.Futures) => new()
    {
        Guid = Guid.NewGuid(),
        Symbol = symbol,
        Spread = spread,
        Type = type,
        ExchangeRateA = MakeLeg(exchangeA),
        ExchangeRateB = MakeLeg(exchangeB),
        ExchangeShort = MakeLeg(exchangeA),
        ExchangeLong = MakeLeg(exchangeB),
    };

    private static TradeOpportunityModel MakeSpreadWithVolumes(string exchangeA, double volumeA, string exchangeB, double volumeB) => new()
    {
        Guid = Guid.NewGuid(),
        Symbol = "BTC/USDT",
        Spread = 1.0,
        Type = SpreadType.Futures,
        ExchangeRateA = MakeLeg(exchangeA, volumeA, volumeA),
        ExchangeRateB = MakeLeg(exchangeB, volumeB, volumeB),
        ExchangeShort = MakeLeg(exchangeA, volumeA, volumeA),
        ExchangeLong = MakeLeg(exchangeB, volumeB, volumeB),
    };

    private static SpreadStatsSnapshotModel MakeSnapshot(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        GeneratedAtUtc = DateTime.UtcNow,
        TotalSpreadsAnalyzed = 1,
        TopSymbolsByAverageSpread = [],
        TopExchangesByCount = [],
        TopExchangePairsByCount = [],
        MedianVolumeByExchange = [],
        SpreadTypeDistribution = [],
        TopSymbolsByCount = [],
    };

    // --- GetLatestAsync: cache-aside ---

    [Fact]
    public async Task GetLatestAsync_CacheHit_ReturnsFromCacheWithoutHittingDb()
    {
        var snapshot = MakeSnapshot();
        _redisDb.Setup(d => d.StringGetAsync((RedisKey)"stats:latest", It.IsAny<CommandFlags>()))
            .ReturnsAsync(Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));

        var result = await _sut.GetLatestAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(snapshot.Id);
        _statsRepo.Verify(r => r.GetLatestAsync(), Times.Never);
    }

    [Fact]
    public async Task GetLatestAsync_CacheMiss_FallsBackToDbAndRepopulatesCache()
    {
        var snapshot = MakeSnapshot();
        _statsRepo.Setup(r => r.GetLatestAsync()).ReturnsAsync(snapshot);

        var result = await _sut.GetLatestAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(snapshot);
        _redisDb.Verify(d => d.StringSetAsync((RedisKey)"stats:latest", It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetLatestAsync_NothingCachedOrPersisted_ReturnsNotFound()
    {
        _statsRepo.Setup(r => r.GetLatestAsync()).ReturnsAsync((SpreadStatsSnapshotModel?)null);

        var result = await _sut.GetLatestAsync();

        result.IsFailed.Should().BeTrue();
    }

    // --- GetByIdAsync: no cache, straight to the repository ---

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsOk()
    {
        var snapshot = MakeSnapshot();
        _statsRepo.Setup(r => r.GetByIdAsync(snapshot.Id)).ReturnsAsync(snapshot);

        var result = await _sut.GetByIdAsync(snapshot.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(snapshot);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsFail()
    {
        _statsRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((SpreadStatsSnapshotModel?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.IsFailed.Should().BeTrue();
    }

    // --- GetSnapshotIndexAsync: cache-aside ---

    [Fact]
    public async Task GetSnapshotIndexAsync_CacheHit_ReturnsFromCacheWithoutHittingDb()
    {
        var index = new List<SnapshotIndexEntry> { new() { Id = Guid.NewGuid(), GeneratedAtUtc = DateTime.UtcNow } };
        _redisDb.Setup(d => d.StringGetAsync((RedisKey)"stats:index", It.IsAny<CommandFlags>()))
            .ReturnsAsync(Newtonsoft.Json.JsonConvert.SerializeObject(index));

        var result = await _sut.GetSnapshotIndexAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(index);
        _statsRepo.Verify(r => r.GetIndexAsync(), Times.Never);
    }

    [Fact]
    public async Task GetSnapshotIndexAsync_CacheMiss_FallsBackToDbAndRepopulatesCache()
    {
        var index = new List<SnapshotIndexEntry> { new() { Id = Guid.NewGuid(), GeneratedAtUtc = DateTime.UtcNow } };
        _statsRepo.Setup(r => r.GetIndexAsync()).ReturnsAsync(index);

        var result = await _sut.GetSnapshotIndexAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(index);
        _redisDb.Verify(d => d.StringSetAsync((RedisKey)"stats:index", It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    // --- GenerateAndPersistSnapshotAsync ---

    // ExchangeRateA/B are assigned by arbitrary scan order upstream, so the same physical pair can
    // show up as (Binance, Bybit) on one document and (Bybit, Binance) on another - this is the one
    // genuinely tricky bit of the aggregation and deserves a dedicated test.
    [Fact]
    public async Task GenerateAndPersistSnapshotAsync_NormalizesReversedExchangePairsIntoOneCount()
    {
        var spreads = new List<TradeOpportunityModel>
        {
            MakeSpread("BTC/USDT", "Binance", "Bybit", 1.0),
            MakeSpread("BTC/USDT", "Bybit", "Binance", 1.2),
            MakeSpread("BTC/USDT", "bybit", "BINANCE", 1.1),
        };
        _spreadRepo.Setup(r => r.GetAllForStatsAsync()).ReturnsAsync(spreads);
        _statsRepo.Setup(r => r.GetIndexAsync()).ReturnsAsync([]);

        var result = await _sut.GenerateAndPersistSnapshotAsync();

        result.Value.TopExchangePairsByCount.Should().ContainSingle();
        result.Value.TopExchangePairsByCount[0].Count.Should().Be(3);
    }

    [Fact]
    public async Task GenerateAndPersistSnapshotAsync_CountsEachExchangeOncePerDocumentAcrossBothSides()
    {
        var spreads = new List<TradeOpportunityModel>
        {
            MakeSpread("BTC/USDT", "Binance", "Bybit", 1.0),
            MakeSpread("ETH/USDT", "Binance", "OKX", 1.0),
        };
        _spreadRepo.Setup(r => r.GetAllForStatsAsync()).ReturnsAsync(spreads);
        _statsRepo.Setup(r => r.GetIndexAsync()).ReturnsAsync([]);

        var result = await _sut.GenerateAndPersistSnapshotAsync();

        result.Value.TopExchangesByCount.Single(e => e.Exchange == "Binance").Count.Should().Be(2);
        result.Value.TopExchangesByCount.Single(e => e.Exchange == "Bybit").Count.Should().Be(1);
        result.Value.TopExchangesByCount.Single(e => e.Exchange == "OKX").Count.Should().Be(1);
    }

    // Median (not mean) so a handful of unusually deep/thin order books don't skew the
    // "typical" liquidity figure for an exchange.
    [Fact]
    public async Task GenerateAndPersistSnapshotAsync_UsesMedianNotMeanForVolumeByExchange()
    {
        var spreads = new List<TradeOpportunityModel>
        {
            MakeSpreadWithVolumes("Binance", 10, "Bybit", 999),
            MakeSpreadWithVolumes("Binance", 20, "Bybit", 999),
            MakeSpreadWithVolumes("Binance", 30, "Bybit", 999),
            MakeSpreadWithVolumes("Binance", 1000, "Bybit", 999), // outlier
        };
        _spreadRepo.Setup(r => r.GetAllForStatsAsync()).ReturnsAsync(spreads);
        _statsRepo.Setup(r => r.GetIndexAsync()).ReturnsAsync([]);

        var result = await _sut.GenerateAndPersistSnapshotAsync();

        var binance = result.Value.MedianVolumeByExchange.Single(e => e.Exchange == "Binance");
        // Median of [10, 20, 30, 1000] is (20+30)/2 = 25 - the mean (265) would be far higher.
        binance.MedianVolume.Should().Be(25);
        binance.SampleCount.Should().Be(4);
    }

    [Fact]
    public async Task GenerateAndPersistSnapshotAsync_PersistsAndCachesTheNewSnapshot()
    {
        _spreadRepo.Setup(r => r.GetAllForStatsAsync()).ReturnsAsync([]);
        _statsRepo.Setup(r => r.GetIndexAsync()).ReturnsAsync([]);

        var result = await _sut.GenerateAndPersistSnapshotAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSpreadsAnalyzed.Should().Be(0);
        _statsRepo.Verify(r => r.InsertSnapshotAsync(It.Is<SpreadStatsSnapshotModel>(m => m.Id == result.Value.Id)), Times.Once);
        _redisDb.Verify(d => d.StringSetAsync((RedisKey)"stats:latest", It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()), Times.Once);
        _redisDb.Verify(d => d.StringSetAsync((RedisKey)"stats:index", It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()), Times.Once);
    }
}
