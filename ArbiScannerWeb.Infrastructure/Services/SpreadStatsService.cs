using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace ArbiScannerWeb.Infrastructure.Services
{
    public class SpreadStatsService : ISpreadStatsService
    {
        private const int TopN = 10;
        private const string LatestCacheKey = "stats:latest";
        private const string IndexCacheKey = "stats:index";
        // Comfortably longer than the daily aggregation cadence so a missed/delayed
        // run degrades gracefully to the Mongo fallback instead of serving stale data.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(30);

        private readonly ITradeOpportunityRepository _tradeOpportunityRepository;
        private readonly ISpreadStatsRepository _statsRepository;
        private readonly IDatabase _redis;
        private readonly ILogger<SpreadStatsService> _logger;

        public SpreadStatsService(
            ITradeOpportunityRepository tradeOpportunityRepository,
            ISpreadStatsRepository statsRepository,
            IConnectionMultiplexer redis,
            ILogger<SpreadStatsService> logger)
        {
            _tradeOpportunityRepository = tradeOpportunityRepository;
            _statsRepository = statsRepository;
            _redis = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<Result<SpreadStatsSnapshotModel>> GetLatestAsync()
        {
            var cached = await _redis.StringGetAsync(LatestCacheKey);
            if (cached.HasValue)
            {
                var fromCache = JsonConvert.DeserializeObject<SpreadStatsSnapshotModel>(cached!);
                if (fromCache != null)
                    return Result.Ok(fromCache);
            }

            var latest = await _statsRepository.GetLatestAsync();
            if (latest == null)
                return Result.Fail<SpreadStatsSnapshotModel>(TypedErrors.NotFound("No spread stats snapshot has been generated yet"));

            await _redis.StringSetAsync(LatestCacheKey, JsonConvert.SerializeObject(latest), CacheTtl);
            return Result.Ok(latest);
        }

        public async Task<Result<SpreadStatsSnapshotModel>> GetByIdAsync(Guid id)
        {
            var snapshot = await _statsRepository.GetByIdAsync(id);
            if (snapshot == null)
                return Result.Fail<SpreadStatsSnapshotModel>(TypedErrors.NotFound($"Spread stats snapshot {id} not found"));

            return Result.Ok(snapshot);
        }

        public async Task<Result<List<SnapshotIndexEntry>>> GetSnapshotIndexAsync()
        {
            var cached = await _redis.StringGetAsync(IndexCacheKey);
            if (cached.HasValue)
            {
                var fromCache = JsonConvert.DeserializeObject<List<SnapshotIndexEntry>>(cached!);
                if (fromCache != null)
                    return Result.Ok(fromCache);
            }

            var index = await _statsRepository.GetIndexAsync();
            await _redis.StringSetAsync(IndexCacheKey, JsonConvert.SerializeObject(index), CacheTtl);
            return Result.Ok(index);
        }

        public async Task<Result<SpreadStatsSnapshotModel>> GenerateAndPersistSnapshotAsync()
        {
            var spreads = await _tradeOpportunityRepository.GetAllForStatsAsync();

            var snapshot = new SpreadStatsSnapshotModel
            {
                Id = Guid.NewGuid(),
                GeneratedAtUtc = DateTime.UtcNow,
                TotalSpreadsAnalyzed = spreads.Count,
                TopSymbolsByAverageSpread = BuildTopSymbolsByAverageSpread(spreads),
                TopExchangesByCount = BuildTopExchangesByCount(spreads),
                TopExchangePairsByCount = BuildTopExchangePairsByCount(spreads),
                MedianVolumeByExchange = BuildMedianVolumeByExchange(spreads),
                SpreadTypeDistribution = BuildSpreadTypeDistribution(spreads),
                TopSymbolsByCount = BuildTopSymbolsByCount(spreads),
            };

            await _statsRepository.InsertSnapshotAsync(snapshot);
            await RefreshCacheAsync(snapshot);

            _logger.LogInformation(
                "Generated spread stats snapshot {Id} at {GeneratedAtUtc:O} from {Count} spreads",
                snapshot.Id, snapshot.GeneratedAtUtc, snapshot.TotalSpreadsAnalyzed);

            return Result.Ok(snapshot);
        }

        private async Task RefreshCacheAsync(SpreadStatsSnapshotModel latest)
        {
            await _redis.StringSetAsync(LatestCacheKey, JsonConvert.SerializeObject(latest), CacheTtl);

            var index = await _statsRepository.GetIndexAsync();
            await _redis.StringSetAsync(IndexCacheKey, JsonConvert.SerializeObject(index), CacheTtl);
        }

        private static List<SymbolAverageSpreadEntry> BuildTopSymbolsByAverageSpread(List<TradeOpportunityModel> spreads)
            => spreads
                .GroupBy(s => s.Symbol)
                .Select(g => new SymbolAverageSpreadEntry
                {
                    Symbol = g.Key,
                    AverageSpreadPercent = g.Average(s => Math.Abs(s.Spread)),
                    SampleCount = g.Count()
                })
                .OrderByDescending(e => e.AverageSpreadPercent)
                .Take(TopN)
                .ToList();

        private static List<ExchangeCountEntry> BuildTopExchangesByCount(List<TradeOpportunityModel> spreads)
            => spreads
                .SelectMany(s => new[] { s.ExchangeRateA.Exchange, s.ExchangeRateB.Exchange }
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                .GroupBy(exchange => exchange, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ExchangeCountEntry { Exchange = g.Key, Count = g.Count() })
                .OrderByDescending(e => e.Count)
                .Take(TopN)
                .ToList();

        // ExchangeRateA/B are assigned by arbitrary scan-order upstream, so the same physical
        // pair can show up as (Binance, Bybit) on one document and (Bybit, Binance) on another.
        // Sorting the two names before grouping collapses both into a single canonical pair.
        private static List<ExchangePairCountEntry> BuildTopExchangePairsByCount(List<TradeOpportunityModel> spreads)
            => spreads
                .Select(s => NormalizePair(s.ExchangeRateA.Exchange, s.ExchangeRateB.Exchange))
                .GroupBy(pair => (A: pair.A.ToLowerInvariant(), B: pair.B.ToLowerInvariant()))
                .Select(g => new ExchangePairCountEntry
                {
                    ExchangeA = g.First().A,
                    ExchangeB = g.First().B,
                    Count = g.Count()
                })
                .OrderByDescending(e => e.Count)
                .Take(TopN)
                .ToList();

        private static (string A, string B) NormalizePair(string exchangeA, string exchangeB)
            => string.Compare(exchangeA, exchangeB, StringComparison.OrdinalIgnoreCase) <= 0
                ? (exchangeA, exchangeB)
                : (exchangeB, exchangeA);

        // VolumeAsk/VolumeBid are each already the liquidity available within +/-0.5% of the
        // current price (computed upstream by ArbitrageScanner's GetLiquidity) — average of the
        // two gives a single per-leg liquidity figure for that exchange. Median (not mean) across
        // occurrences of that exchange, since a handful of unusually deep/thin order books would
        // otherwise skew the "typical" liquidity figure.
        private static List<ExchangeMedianVolumeEntry> BuildMedianVolumeByExchange(List<TradeOpportunityModel> spreads)
            => spreads
                .SelectMany(s => new[]
                {
                    (Exchange: s.ExchangeRateA.Exchange, Volume: (s.ExchangeRateA.VolumeAsk + s.ExchangeRateA.VolumeBid) / 2.0),
                    (Exchange: s.ExchangeRateB.Exchange, Volume: (s.ExchangeRateB.VolumeAsk + s.ExchangeRateB.VolumeBid) / 2.0),
                })
                .GroupBy(x => x.Exchange, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ExchangeMedianVolumeEntry
                {
                    Exchange = g.Key,
                    MedianVolume = Median(g.Select(x => x.Volume)),
                    SampleCount = g.Count()
                })
                .OrderByDescending(e => e.MedianVolume)
                .Take(TopN)
                .ToList();

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }

        private static List<SpreadTypeCountEntry> BuildSpreadTypeDistribution(List<TradeOpportunityModel> spreads)
            => spreads
                .GroupBy(s => s.Type)
                .Select(g => new SpreadTypeCountEntry { Type = g.Key, Count = g.Count() })
                .OrderByDescending(e => e.Count)
                .ToList();

        private static List<SymbolCountEntry> BuildTopSymbolsByCount(List<TradeOpportunityModel> spreads)
            => spreads
                .GroupBy(s => s.Symbol)
                .Select(g => new SymbolCountEntry { Symbol = g.Key, Count = g.Count() })
                .OrderByDescending(e => e.Count)
                .Take(TopN)
                .ToList();
    }
}
