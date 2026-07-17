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

        public async Task<Result<TradeOpportunityDetailsDTO>> GetSpreadInfo(string id)
        {
            if (!await _subscriptionService.CheckIfUserHasActiveSubscriptionAsync())
            {
                return Result.Fail<TradeOpportunityDetailsDTO>(TypedErrors.Forbidden("No active subscription"));
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

                    var dto = new TradeOpportunityDetailsDTO { PositionModel = spread, Tickers = tickers, GroupName = GetGroupNameBasedOnPosition(spread) };
                    await EnrichWithExchangeLinksAsync(dto);
                    return Result.Ok(dto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get spread info for {Id}", id);
                return Result.Fail(ex.Message);
            }
            return Result.Fail<TradeOpportunityDetailsDTO>(TypedErrors.NotFound("Spread not found"));
        }

        public async Task<Result<List<TradeOpportunityDetailsDTO>>> GetSpreadsForUser(string userId)
        {
            if (!await _subscriptionService.CheckIfUserHasActiveSubscriptionAsync())
            {
                return Result.Fail<List<TradeOpportunityDetailsDTO>>(TypedErrors.Forbidden("No active subscription"));
            }
            try
            {
                long userSettingsId;
                bool futuresSpread, fundingSpread, spotSpread;

                await using (var context = await _dbContextFactory.CreateDbContextAsync())
                {
                    var user = await context.Users.FindAsync(userId);
                    if (user == null)
                        return Result.Fail<List<TradeOpportunityDetailsDTO>>(TypedErrors.NotFound("User not found"));

                    var userSettings = await context.UsersSettings.FirstOrDefaultAsync(x => x.Id == user.UserSettingsId);
                    if (userSettings == null)
                        return Result.Fail<List<TradeOpportunityDetailsDTO>>(TypedErrors.NotFound("Telegram user not found"));

                    userSettingsId = user.UserSettingsId;
                    futuresSpread = userSettings.FuturesSpread;
                    fundingSpread = userSettings.FundingSpread;
                    spotSpread = userSettings.SpotSpread;
                }

                var spreadTypes = new List<SpreadType>();
                if (futuresSpread) spreadTypes.Add(SpreadType.Futures);
                if (fundingSpread) spreadTypes.Add(SpreadType.Funding);
                if (spotSpread) spreadTypes.Add(SpreadType.Spot);

                if (spreadTypes.Count == 0)
                    return Result.Ok(new List<TradeOpportunityDetailsDTO>());

                var tasks = spreadTypes.Select(type => GetCurrentSpreadsForUser(userSettingsId, type)).ToList();
                var results = await Task.WhenAll(tasks);

                var failures = results.Where(r => r.IsFailed).ToList();
                if (failures.Count == results.Length)
                    return Result.Fail<List<TradeOpportunityDetailsDTO>>(failures[0].Errors[0]);

                var combined = results
                    .Where(r => r.IsSuccess)
                    .SelectMany(r => r.Value)
                    .ToList();

                var dtoList = combined
                    .Select(x => new TradeOpportunityDetailsDTO
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

        private async Task EnrichWithExchangeLinksAsync(TradeOpportunityDetailsDTO dto)
        {
            var allLinks = await _exchangeLinkRepo.GetAllAsync();
            PopulateExchangeLinks(dto, allLinks);
        }

        private static void PopulateExchangeLinks(TradeOpportunityDetailsDTO dto, List<ExchangeLinkModel> allLinks)
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
