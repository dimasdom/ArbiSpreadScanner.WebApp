using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Services
{
    public class TradeOpportunityService : ITradeOpportunityService
    {
        /// <summary>Recommendation/analysis thresholds, shared between GetRecommendedSpreads'
        /// ranking and GetSpreadInfo's Analysis - keeping them as one set of constants avoids
        /// the two features silently drifting to different "what counts as a good spread"
        /// definitions.</summary>
        private const double MinRecommendedSpreadPercent = 1.0;
        private const double MaxCostRatio = 0.10;
        private const double FallingTrendRelativeThreshold = 0.20;


        private readonly ISubscriptionService _subscriptionService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly ITradeOpportunityRepository _spreadRepo;
        private readonly ITradeOpportunityTickerRepository _tickerRepo;
        private readonly IExchangeLinkRepository _exchangeLinkRepo;
        private readonly ILogger<TradeOpportunityService> _logger;

        public TradeOpportunityService(
            IRealtimeNotifier realtimeNotifier,
            IDbContextFactory<AppDbContext> dbContextFactory,
            ISubscriptionService subscriptionService,
            ITradeOpportunityRepository spreadRepo,
            ITradeOpportunityTickerRepository tickerRepo,
            IExchangeLinkRepository exchangeLinkRepo,
            ILogger<TradeOpportunityService> logger)
        {
            _realtimeNotifier = realtimeNotifier;
            _dbContextFactory = dbContextFactory;
            _subscriptionService = subscriptionService;
            _spreadRepo = spreadRepo;
            _tickerRepo = tickerRepo;
            _exchangeLinkRepo = exchangeLinkRepo;
            _logger = logger;
        }

        public async Task<Result> AddSpread(TradeOpportunityModel model)
        {
            try
            {
                model.DateTime = DateTime.UtcNow;
                await _spreadRepo.UpsertAsync(model);

                var ticker = new TradeOpportunityTickerModel(model);
                await _tickerRepo.InsertAsync(ticker);

                _ = _realtimeNotifier.NotifyGroupAsync(GetGroupNameBasedOnPosition(model), new MessageDto() { TradeOpportunity = model });
                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add spread {Guid}", model.Guid);
                return Result.Fail(ex.Message);
            }
        }

        public async Task<Result> CloseSpread(TradeOpportunityModel model)
        {
            try
            {
                model.DateTime = DateTime.UtcNow;
                _ = _realtimeNotifier.NotifyGroupAsync(GetGroupNameBasedOnPosition(model), new MessageDto() { TradeOpportunity = model });

                await _spreadRepo.SetStatusClosedAsync(model.Guid);
                await _tickerRepo.DeleteAllByGuidAsync(model.Guid);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close spread {Guid}", model.Guid);
                return Result.Fail(ex.Message);
            }
        }

        public async Task<Result<List<TradeOpportunityModel>>> GetAllSpreads()
        {
            try
            {
                return Result.Ok(await _spreadRepo.GetAllAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all spreads");
                return Result.Fail(ex.Message);
            }
        }

        public async Task<Result<TradeOpportunityDetailsDto>> GetSpreadInfo(string id)
        {
            if (!await _subscriptionService.CheckIfUserHasActiveSubscriptionAsync())
            {
                return Result.Fail<TradeOpportunityDetailsDto>(TypedErrors.Forbidden("No active subscription"));
            }
            try
            {
                var guid = new Guid(id);
                var spread = await _spreadRepo.GetByGuidAsync(guid);
                var latestTicker = await _tickerRepo.GetLatestByGuidAsync(guid);

                if (spread != null && latestTicker != null)
                {
                    var remaining = await _tickerRepo.GetRemainingWithoutOrderBookAsync(guid, skip: 1, limit: 49);
                    var tickers = new List<TradeOpportunityTickerModel> { latestTicker };
                    tickers.AddRange(remaining);

                    var dto = new TradeOpportunityDetailsDto { PositionModel = spread, Tickers = tickers, GroupName = GetGroupNameBasedOnPosition(spread) };
                    await EnrichWithExchangeLinksAsync(dto);
                    dto.Analysis = BuildAnalysis(spread, tickers);
                    return Result.Ok(dto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get spread info for {Id}", id);
                return Result.Fail(ex.Message);
            }
            return Result.Fail<TradeOpportunityDetailsDto>(TypedErrors.NotFound("Spread not found"));
        }

        public async Task<Result<List<TradeOpportunityDetailsDto>>> GetSpreadsForUser(string userId)
        {
            if (!await _subscriptionService.CheckIfUserHasActiveSubscriptionAsync())
            {
                return Result.Fail<List<TradeOpportunityDetailsDto>>(TypedErrors.Forbidden("No active subscription"));
            }
            try
            {
                var settingsResult = await GetUserSpreadTypesAsync(userId);
                if (settingsResult.IsFailed)
                    return settingsResult.ToResult<List<TradeOpportunityDetailsDto>>();

                var (userSettingsId, spreadTypes) = settingsResult.Value;
                if (spreadTypes.Count == 0)
                    return Result.Ok(new List<TradeOpportunityDetailsDto>());

                var tasks = spreadTypes.Select(type => GetCurrentSpreadsForUser(userSettingsId, type)).ToList();
                var results = await Task.WhenAll(tasks);

                var failures = results.Where(r => r.IsFailed).ToList();
                if (failures.Count == results.Length)
                    return Result.Fail<List<TradeOpportunityDetailsDto>>(failures[0].Errors[0]);

                var combined = results
                    .Where(r => r.IsSuccess)
                    .SelectMany(r => r.Value)
                    .ToList();

                var dtoList = combined
                    .Select(x => new TradeOpportunityDetailsDto
                    {
                        PositionModel = x,
                        Tickers = new List<TradeOpportunityTickerModel>(),
                        GroupName = GetGroupNameBasedOnPosition(x)
                    })
                    .ToList();

                var allLinks = await _exchangeLinkRepo.GetAllAsync();
                foreach (var dto in dtoList)
                    PopulateExchangeLinks(dto, allLinks);

                return Result.Ok(dtoList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get spreads for user {UserId}", userId);
                return Result.Fail(ex.Message);
            }
        }

        public async Task<Result<List<RecommendedSpreadDto>>> GetRecommendedSpreads(string userId)
        {
            if (!await _subscriptionService.CheckIfUserHasActiveSubscriptionAsync())
            {
                return Result.Fail<List<RecommendedSpreadDto>>(TypedErrors.Forbidden("No active subscription"));
            }
            try
            {
                var settingsResult = await GetUserSpreadTypesAsync(userId);
                if (settingsResult.IsFailed)
                    return settingsResult.ToResult<List<RecommendedSpreadDto>>();

                var (userSettingsId, spreadTypes) = settingsResult.Value;
                if (spreadTypes.Count == 0)
                    return Result.Ok(new List<RecommendedSpreadDto>());

                var allLinks = await _exchangeLinkRepo.GetAllAsync();
                var recommended = new List<RecommendedSpreadDto>();

                foreach (var type in spreadTypes)
                {
                    var candidatesResult = await GetCurrentSpreadsForUser(userSettingsId, type);
                    if (candidatesResult.IsFailed)
                        continue;

                    var best = candidatesResult.Value
                        .Where(x => Math.Abs(x.Spread) >= MinRecommendedSpreadPercent)
                        .OrderBy(GetRecommendationCostScore)
                        .ThenByDescending(x => Math.Abs(x.Spread))
                        .Take(2);

                    foreach (var model in best)
                    {
                        var dto = new TradeOpportunityDetailsDto
                        {
                            PositionModel = model,
                            Tickers = [],
                            GroupName = GetGroupNameBasedOnPosition(model)
                        };
                        PopulateExchangeLinks(dto, allLinks);
                        recommended.Add(new RecommendedSpreadDto { Details = dto, Category = type });
                    }
                }

                return Result.Ok(recommended);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recommended spreads for user {UserId}", userId);
                return Result.Fail(ex.Message);
            }
        }

        private async Task<Result<(int UserSettingsId, List<SpreadType> SpreadTypes)>> GetUserSpreadTypesAsync(string userId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var user = await context.Users.FindAsync(userId);
            if (user == null)
                return Result.Fail<(int, List<SpreadType>)>(TypedErrors.NotFound("User not found"));

            var userSettings = await context.UsersSettings.FirstOrDefaultAsync(x => x.Id == user.UserSettingsId);
            if (userSettings == null)
                return Result.Fail<(int, List<SpreadType>)>(TypedErrors.NotFound("Telegram user not found"));

            var spreadTypes = new List<SpreadType>();
            if (userSettings.FuturesSpread) spreadTypes.Add(SpreadType.Futures);
            if (userSettings.FundingSpread) spreadTypes.Add(SpreadType.Funding);
            if (userSettings.SpotSpread) spreadTypes.Add(SpreadType.Spot);

            return Result.Ok((user.UserSettingsId, spreadTypes));
        }

        /// <summary>Ranking score for GetRecommendedSpreads, ascending (lower = better): total
        /// slippage plus a funding-cost penalty. Funding-type spreads get no funding penalty -
        /// funding income is that category's entire point, not a cost to minimize.</summary>
        private static double GetRecommendationCostScore(TradeOpportunityModel model)
        {
            var fundingCost = model.Type == SpreadType.Funding ? 0 : GetCombinedFundingExposure(model);
            return GetTotalSlippage(model) + fundingCost;
        }

        private static double GetTotalSlippage(TradeOpportunityModel model) =>
            model.ExchangeLong.SummarySlipage + model.ExchangeShort.SummarySlipage;

        /// <summary>Combined absolute funding-rate exposure across both legs, except for Spot
        /// spreads where only the short (futures/perp) leg has funding by definition - the
        /// long/spot leg never does.</summary>
        private static double GetCombinedFundingExposure(TradeOpportunityModel model) => model.Type == SpreadType.Spot
            ? FundingRatePercent(model.ExchangeShort.FundingRateValue)
            : FundingRatePercent(model.ExchangeLong.FundingRateValue) + FundingRatePercent(model.ExchangeShort.FundingRateValue);

        /// <summary>FundingRateValue is the raw exchange funding-rate fraction (e.g. 0.0001 -
        /// see ExchangeService.BuildMainExchangeRate), roughly 1000x smaller than
        /// Spread/SummarySlipage/SummaryTarrif, which are all percentage-point scale (computed
        /// with "* 100" - see FuturesPositionCalculatorService.CalculateSpreadFor/CalculateSlippage).
        /// Scale it up here so funding cost isn't numerically negligible against those.</summary>
        private static double FundingRatePercent(double? value) => Math.Abs(value ?? 0) * 100;

        /// <summary>Builds GetSpreadInfo's Analysis block: a Recommended verdict against the
        /// same &gt;=1%-spread rule GetRecommendedSpreads uses, plus a 10%-of-spread cost
        /// check. For Funding spreads the funding rate is the return being evaluated, not a
        /// cost against it, so - same as GetRecommendationCostScore's ranking - only fee and
        /// slippage count there; every other type counts funding, fee and slippage together.</summary>
        private static SpreadAnalysisDto BuildAnalysis(TradeOpportunityModel model, List<TradeOpportunityTickerModel> tickers)
        {
            var absSpread = Math.Abs(model.Spread);
            var meetsMinSpread = absSpread >= MinRecommendedSpreadPercent;

            var isFunding = model.Type == SpreadType.Funding;
            var fundingCost = isFunding ? 0 : GetCombinedFundingExposure(model);
            var costTotal = GetTotalSlippage(model) + model.SummaryTarrif + fundingCost;
            var costRatio = absSpread > 0 ? costTotal / absSpread : double.PositiveInfinity;
            var withinCostThreshold = costRatio <= MaxCostRatio;
            var costLabel = isFunding ? "fee and slippage" : "funding, fee and slippage";

            var reasons = new List<string>
            {
                meetsMinSpread
                    ? $"Spread is {absSpread:0.##}%, at or above the {MinRecommendedSpreadPercent:0.#}% minimum."
                    : $"Spread is {absSpread:0.##}%, below the {MinRecommendedSpreadPercent:0.#}% minimum.",
                withinCostThreshold
                    ? $"Combined {costLabel} cost is {costRatio * 100:0}% of the spread, within the {MaxCostRatio * 100:0}% limit."
                    : $"Combined {costLabel} cost is {costRatio * 100:0}% of the spread, above the {MaxCostRatio * 100:0}% limit.",
            };

            return new SpreadAnalysisDto
            {
                Recommended = meetsMinSpread && withinCostThreshold,
                Reasons = reasons,
                TrendWarning = isFunding ? BuildTrendWarning(tickers) : null,
            };
        }

        /// <summary>Tickers arrive newest-first (latest ticker prepended, then the remaining
        /// window in descending DateTime order - see GetSpreadInfo/GetRemainingWithoutOrderBookAsync),
        /// so tickers[0] is newest and tickers[^1] is the oldest point in the window.</summary>
        private static string? BuildTrendWarning(List<TradeOpportunityTickerModel> tickers)
        {
            if (tickers.Count < 3) return null;

            var newest = tickers[0].Spread;
            var oldest = tickers[^1].Spread;
            if (oldest == 0) return null;

            var relativeChange = (newest - oldest) / Math.Abs(oldest);
            return relativeChange <= -FallingTrendRelativeThreshold
                ? "Spread has been falling - by the time funding pays out, fees and slippage may exceed what you collect."
                : null;
        }

        public async Task<Result> UpdateSpread(TradeOpportunityModel model)
        {
            try
            {
                model.DateTime = DateTime.UtcNow;
                var existing = await _spreadRepo.GetByGuidAsync(model.Guid);

                if (existing is not null)
                {
                    if (existing.OrderStatus == OrderStatus.Closed)
                        return Result.Ok();

                    existing.ExchangeRateA.ExchangeRate = model.ExchangeRateA.ExchangeRate;
                    existing.ExchangeRateB.ExchangeRate = model.ExchangeRateB.ExchangeRate;
                    existing.ExchangeLong.ExchangeRate = model.ExchangeLong.ExchangeRate;
                    existing.ExchangeShort.ExchangeRate = model.ExchangeShort.ExchangeRate;
                    existing.ExchangeRateA.FundingRateValue = model.ExchangeRateA.FundingRateValue ?? existing.ExchangeRateA.FundingRateValue;
                    existing.ExchangeRateB.FundingRateValue = model.ExchangeRateB.FundingRateValue ?? existing.ExchangeRateB.FundingRateValue;
                    existing.ExchangeLong.FundingRateValue = model.ExchangeLong.FundingRateValue ?? existing.ExchangeLong.FundingRateValue;
                    existing.ExchangeShort.FundingRateValue = model.ExchangeShort.FundingRateValue ?? existing.ExchangeShort.FundingRateValue;
                    existing.Spread = model.Spread;
                    existing.PossibleProfit = model.PossibleProfit;
                    existing.SummaryTarrif = model.SummaryTarrif;
                    existing.DateTime = model.DateTime;

                    await _spreadRepo.ReplaceAsync(existing);

                    var ticker = new TradeOpportunityTickerModel(model);
                    _ = _realtimeNotifier.NotifyGroupAsync(GetGroupNameBasedOnPosition(model), new MessageDto() { Ticker = ticker });
                    await _tickerRepo.InsertAsync(ticker);
                }
                else
                {
                    _logger.LogWarning("Spread with Guid {Guid} not found for update", model.Guid);
                    await AddSpread(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update spread {Guid}", model.Guid);
                return Result.Fail(ex.Message);
            }
            return Result.Ok();
        }

        private static string GetGroupNameBasedOnPosition(TradeOpportunityModel model)
            => model.Guid.ToString().ToLowerInvariant();

        private async Task EnrichWithExchangeLinksAsync(TradeOpportunityDetailsDto dto)
        {
            var allLinks = await _exchangeLinkRepo.GetAllAsync();
            PopulateExchangeLinks(dto, allLinks);
        }

        private static void PopulateExchangeLinks(TradeOpportunityDetailsDto dto, List<ExchangeLinkModel> allLinks)
        {
            var model = dto.PositionModel;
            var symbol = model.Symbol;
            var type = model.Type;

            var shortLink = allLinks.FirstOrDefault(l =>
                model.ExchangeShort.Exchange.StartsWith(l.Exchange, StringComparison.OrdinalIgnoreCase));
            var longLink = allLinks.FirstOrDefault(l =>
                model.ExchangeLong.Exchange.StartsWith(l.Exchange, StringComparison.OrdinalIgnoreCase));

            var shortType = type == SpreadType.Spot ? SpreadType.Futures : type;
            dto.ShortExchangeUrl = shortLink?.BuildUrl(symbol, shortType);
            dto.LongExchangeUrl = longLink?.BuildUrl(symbol, type);
        }

        public async Task<Result<List<TradeOpportunityModel>>> GetCurrentFuturesSpreads(long chatId = 0)
            => await GetCurrentSpreadsForUser(chatId, SpreadType.Futures);

        public async Task<Result<List<TradeOpportunityModel>>> GetCurrentFundingSpreads(long chatId = 0)
            => await GetCurrentSpreadsForUser(chatId, SpreadType.Funding);

        public async Task<Result<List<TradeOpportunityModel>>> GetCurrentSpotSpreads(long chatId = 0)
            => await GetCurrentSpreadsForUser(chatId, SpreadType.Spot);

        private async Task<Result<List<TradeOpportunityModel>>> GetCurrentSpreadsForUser(long chatId, SpreadType type)
        {
            try
            {
                if (chatId != 0)
                {
                    await using var context = await _dbContextFactory.CreateDbContextAsync();
                    var userSettings = await context.UsersSettings
                        .Include(u => u.Exchanges).ThenInclude(e => e.Exchange)
                        .FirstOrDefaultAsync(x => x.Id == chatId);
                    if (userSettings == null)
                        return Result.Fail<List<TradeOpportunityModel>>(TypedErrors.NotFound("Telegram user not found"));

                    var exchangeNames = userSettings.Exchanges.Select(e => e.Exchange.Name).ToList();
                    var candidates = await _spreadRepo.GetOpenByTypeAsync(type);
                    var filtered = candidates
                        .Where(x => Math.Abs(x.StartSpread) >= userSettings.SpreadSize
                            && exchangeNames.Any(ex => x.ExchangeRateA.Exchange.StartsWith(ex, StringComparison.OrdinalIgnoreCase))
                            && exchangeNames.Any(ex => x.ExchangeRateB.Exchange.StartsWith(ex, StringComparison.OrdinalIgnoreCase))
                            && UserSettingsModel.IsVolumeValid(userSettings, x))
                        .ToList();
                    return Result.Ok(filtered);
                }

                return Result.Ok(await _spreadRepo.GetByTypeAsync(type));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get spreads of type {Type} for chatId {ChatId}", type, chatId);
                return Result.Fail(ex.Message);
            }
        }
    }
}
