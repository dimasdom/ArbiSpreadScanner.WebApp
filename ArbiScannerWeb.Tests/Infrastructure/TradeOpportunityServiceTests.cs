using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class TradeOpportunityServiceTests
{
    private readonly Mock<IRealtimeNotifier> _notifier = new();
    private readonly Mock<IDbContextFactory<AppDbContext>> _ctxFactory = new();
    private readonly Mock<ISubscriptionService> _subscriptions = new();
    private readonly Mock<ITradeOpportunityRepository> _spreadRepo = new();
    private readonly Mock<ITradeOpportunityTickerRepository> _tickerRepo = new();
    private readonly Mock<IExchangeLinkRepository> _linkRepo = new();
    private readonly TradeOpportunityService _sut;

    public TradeOpportunityServiceTests()
    {
        _notifier.Setup(n => n.NotifyGroupAsync(It.IsAny<string>(), It.IsAny<MessageDto>()))
            .Returns(Task.CompletedTask);

        _sut = new TradeOpportunityService(
            _notifier.Object,
            _ctxFactory.Object,
            _subscriptions.Object,
            _spreadRepo.Object,
            _tickerRepo.Object,
            _linkRepo.Object,
            NullLogger<TradeOpportunityService>.Instance);
    }

    private static TradeOpportunityModel MakeModel(Guid? guid = null) => new()
    {
        Guid = guid ?? Guid.NewGuid(),
        Symbol = "BTC/USDT",
        Spread = 1.5,
        Type = SpreadType.Futures,
        ExchangeRateA = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "Binance" },
        ExchangeRateB = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "Bybit" },
        ExchangeShort = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "Binance" },
        ExchangeLong  = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "Bybit" }
    };

    [Fact]
    public async Task AddSpread_InsertsToReposAndNotifiesClient_ReturnsOk()
    {
        var model = MakeModel();
        _spreadRepo.Setup(r => r.UpsertAsync(model)).Returns(Task.CompletedTask);
        _tickerRepo.Setup(r => r.InsertAsync(It.IsAny<TradeOpportunityTickerModel>())).Returns(Task.CompletedTask);

        var result = await _sut.AddSpread(model);

        result.IsSuccess.Should().BeTrue();
        model.DateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _spreadRepo.Verify(r => r.UpsertAsync(model), Times.Once);
        _tickerRepo.Verify(r => r.InsertAsync(It.IsAny<TradeOpportunityTickerModel>()), Times.Once);
        _notifier.Verify(n => n.NotifyGroupAsync(It.IsAny<string>(), It.IsAny<MessageDto>()), Times.Once);
    }

    [Fact]
    public async Task AddSpread_RepositoryThrows_ReturnsFail()
    {
        var model = MakeModel();
        _spreadRepo.Setup(r => r.UpsertAsync(model)).ThrowsAsync(new Exception("db down"));

        var result = await _sut.AddSpread(model);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("db down");
    }

    [Fact]
    public async Task CloseSpread_SetsStatusClosedDeletesTickersAndNotifies_ReturnsOk()
    {
        var model = MakeModel();
        _spreadRepo.Setup(r => r.SetStatusClosedAsync(model.Guid)).Returns(Task.CompletedTask);
        _tickerRepo.Setup(r => r.DeleteAllByGuidAsync(model.Guid)).Returns(Task.CompletedTask);

        var result = await _sut.CloseSpread(model);

        result.IsSuccess.Should().BeTrue();
        _spreadRepo.Verify(r => r.SetStatusClosedAsync(model.Guid), Times.Once);
        _tickerRepo.Verify(r => r.DeleteAllByGuidAsync(model.Guid), Times.Once);
        _notifier.Verify(n => n.NotifyGroupAsync(It.IsAny<string>(), It.IsAny<MessageDto>()), Times.Once);
    }

    [Fact]
    public async Task CloseSpread_RepositoryThrows_ReturnsFail()
    {
        var model = MakeModel();
        _spreadRepo.Setup(r => r.SetStatusClosedAsync(model.Guid)).ThrowsAsync(new Exception("timeout"));

        var result = await _sut.CloseSpread(model);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllSpreads_ReturnsRepoContent()
    {
        var spreads = new List<TradeOpportunityModel> { MakeModel(), MakeModel() };
        _spreadRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(spreads);

        var result = await _sut.GetAllSpreads();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllSpreads_RepositoryThrows_ReturnsFail()
    {
        _spreadRepo.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("mongo down"));

        var result = await _sut.GetAllSpreads();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSpread_ExistingOpenSpread_UpdatesFieldsAndNotifies_ReturnsOk()
    {
        var guid = Guid.NewGuid();
        var incoming = MakeModel(guid);
        incoming.Spread = 2.5;
        incoming.PossibleProfit = 100;

        var existing = MakeModel(guid);
        existing.OrderStatus = OrderStatus.Open;

        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(existing);
        _spreadRepo.Setup(r => r.ReplaceAsync(existing)).Returns(Task.CompletedTask);
        _tickerRepo.Setup(r => r.InsertAsync(It.IsAny<TradeOpportunityTickerModel>())).Returns(Task.CompletedTask);

        var result = await _sut.UpdateSpread(incoming);

        result.IsSuccess.Should().BeTrue();
        existing.Spread.Should().Be(2.5);
        existing.PossibleProfit.Should().Be(100);
        _spreadRepo.Verify(r => r.ReplaceAsync(existing), Times.Once);
        _notifier.Verify(n => n.NotifyGroupAsync(It.IsAny<string>(), It.IsAny<MessageDto>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSpread_ExistingClosedSpread_SkipsUpdateAndReturnsOk()
    {
        var guid = Guid.NewGuid();
        var existing = MakeModel(guid);
        existing.OrderStatus = OrderStatus.Closed;

        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(existing);

        var result = await _sut.UpdateSpread(MakeModel(guid));

        result.IsSuccess.Should().BeTrue();
        _spreadRepo.Verify(r => r.ReplaceAsync(It.IsAny<TradeOpportunityModel>()), Times.Never);
        _notifier.Verify(n => n.NotifyGroupAsync(It.IsAny<string>(), It.IsAny<MessageDto>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSpread_SpreadNotFound_FallsBackToAddSpread()
    {
        var model = MakeModel();
        _spreadRepo.Setup(r => r.GetByGuidAsync(model.Guid)).ReturnsAsync((TradeOpportunityModel?)null);
        _spreadRepo.Setup(r => r.UpsertAsync(It.IsAny<TradeOpportunityModel>())).Returns(Task.CompletedTask);
        _tickerRepo.Setup(r => r.InsertAsync(It.IsAny<TradeOpportunityTickerModel>())).Returns(Task.CompletedTask);

        var result = await _sut.UpdateSpread(model);

        result.IsSuccess.Should().BeTrue();
        _spreadRepo.Verify(r => r.UpsertAsync(It.IsAny<TradeOpportunityModel>()), Times.Once);
    }

    [Fact]
    public async Task GetSpreadInfo_NoActiveSubscription_ReturnsFail()
    {
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(false);

        var result = await _sut.GetSpreadInfo(Guid.NewGuid().ToString());

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("subscription");
    }

    [Fact]
    public async Task GetSpreadInfo_SpreadAndTickerFound_ReturnsDtoWithGroupName()
    {
        var guid = Guid.NewGuid();
        var model = MakeModel(guid);
        var ticker = new TradeOpportunityTickerModel(model);

        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(model);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync(ticker);
        _tickerRepo.Setup(r => r.GetRemainingWithoutOrderBookAsync(guid, 1, 49))
            .ReturnsAsync([]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value.GroupName.Should().Be(guid.ToString().ToLowerInvariant());
        result.Value.PositionModel.Should().BeSameAs(model);
    }

    [Fact]
    public async Task GetSpreadInfo_InvalidGuid_ReturnsFail()
    {
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        var result = await _sut.GetSpreadInfo("not-a-guid");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetSpreadInfo_SpreadNotFound_ReturnsNotFoundFail()
    {
        var guid = Guid.NewGuid();
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync((TradeOpportunityModel?)null);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync((TradeOpportunityTickerModel?)null);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetSpreadInfo_RepositoryThrows_ReturnsFail()
    {
        var guid = Guid.NewGuid();
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ThrowsAsync(new Exception("mongo down"));

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentFuturesSpreads_DelegatesWithFuturesType()
    {
        _spreadRepo.Setup(r => r.GetByTypeAsync(SpreadType.Futures)).ReturnsAsync(new List<TradeOpportunityModel>());

        var result = await _sut.GetCurrentFuturesSpreads();

        result.IsSuccess.Should().BeTrue();
        _spreadRepo.Verify(r => r.GetByTypeAsync(SpreadType.Futures), Times.Once);
    }

    [Fact]
    public async Task GetCurrentFundingSpreads_DelegatesWithFundingType()
    {
        _spreadRepo.Setup(r => r.GetByTypeAsync(SpreadType.Funding)).ReturnsAsync(new List<TradeOpportunityModel>());

        var result = await _sut.GetCurrentFundingSpreads();

        result.IsSuccess.Should().BeTrue();
        _spreadRepo.Verify(r => r.GetByTypeAsync(SpreadType.Funding), Times.Once);
    }

    [Fact]
    public async Task GetCurrentSpotSpreads_DelegatesWithSpotType()
    {
        _spreadRepo.Setup(r => r.GetByTypeAsync(SpreadType.Spot)).ReturnsAsync(new List<TradeOpportunityModel>());

        var result = await _sut.GetCurrentSpotSpreads();

        result.IsSuccess.Should().BeTrue();
        _spreadRepo.Verify(r => r.GetByTypeAsync(SpreadType.Spot), Times.Once);
    }

    [Fact]
    public async Task GetCurrentFuturesSpreads_ChatIdZero_UsesGetByTypeAsync()
    {
        _spreadRepo.Setup(r => r.GetByTypeAsync(SpreadType.Futures)).ThrowsAsync(new Exception("boom"));

        var result = await _sut.GetCurrentFuturesSpreads(0);

        result.IsFailed.Should().BeTrue();
    }

    private static AppDbContext CreateSharedContext(string dbName)
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        return new AppDbContext(options);
    }

    private void SetupContextFactory(string dbName) =>
        _ctxFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSharedContext(dbName));

    /// <summary>A candidate that passes GetCurrentSpreadsForUser's own volume-validity check
    /// (VolumeAsk/VolumeBid &gt; PositionSize*3) - GetRecommendedSpreads tests need this since
    /// they exercise that filter too, on top of the new ranking logic.</summary>
    private static TradeOpportunityModel MakeEligibleModel(Guid? guid = null)
    {
        var model = MakeModel(guid);
        model.ExchangeRateA.VolumeAsk = 1000;
        model.ExchangeRateA.VolumeBid = 1000;
        model.ExchangeRateB.VolumeAsk = 1000;
        model.ExchangeRateB.VolumeBid = 1000;
        return model;
    }

    /// <summary>Seeds a UserSettings/Account pair with both "binance" and "bybit" linked
    /// (matching MakeModel's ExchangeRateA/B exchange names) so GetCurrentSpreadsForUser's
    /// exchange-allowlist filter doesn't reject every candidate.</summary>
    private static async Task SeedEligibleUserAsync(
        string dbName, int userSettingsId, string accountId, Action<UserSettingsModel> configure)
    {
        using var seed = CreateSharedContext(dbName);
        var userSettings = new UserSettingsModel { Id = userSettingsId, AccountId = accountId, PositionSize = 10 };
        configure(userSettings);
        userSettings.Exchanges.Add(new UserExchangeModel
        {
            UserAccountId = accountId,
            Exchange = new ExchangeModel { Id = userSettingsId * 10 + 1, Name = "binance" },
        });
        userSettings.Exchanges.Add(new UserExchangeModel
        {
            UserAccountId = accountId,
            Exchange = new ExchangeModel { Id = userSettingsId * 10 + 2, Name = "bybit" },
        });
        seed.UsersSettings.Add(userSettings);
        seed.Users.Add(new AccountModel { Id = accountId, UserSettingsId = userSettingsId, UserSettings = null! });
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCurrentFuturesSpreads_ChatIdSet_UserSettingsNotFound_ReturnsFail()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);

        var result = await _sut.GetCurrentFuturesSpreads(999);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetCurrentFuturesSpreads_ChatIdSet_FiltersBySpreadSizeAndExchangesAndVolume()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        using (var seed = CreateSharedContext(dbName))
        {
            var exchange = new ExchangeModel { Id = 1, Name = "binance" };
            var userSettings = new UserSettingsModel { Id = 42, AccountId = "acc1", SpreadSize = 1.0, PositionSize = 10 };
            userSettings.Exchanges.Add(new UserExchangeModel { UserAccountId = "acc1", Exchange = exchange });
            seed.UsersSettings.Add(userSettings);
            await seed.SaveChangesAsync();
        }

        var matching = MakeModel();
        matching.StartSpread = 2.0;
        matching.ExchangeRateA = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "binance", VolumeAsk = 1000, VolumeBid = 1000 };
        matching.ExchangeRateB = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "binance", VolumeAsk = 1000, VolumeBid = 1000 };

        var nonMatching = MakeModel();
        nonMatching.StartSpread = 0.1;

        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures))
            .ReturnsAsync(new List<TradeOpportunityModel> { matching, nonMatching });

        var result = await _sut.GetCurrentFuturesSpreads(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(x => x.Guid == matching.Guid);
    }

    [Fact]
    public async Task GetSpreadsForUser_NoActiveSubscription_ReturnsFail()
    {
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(false);

        var result = await _sut.GetSpreadsForUser("u1");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetSpreadsForUser_UserNotFound_ReturnsFail()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        var result = await _sut.GetSpreadsForUser("missing");

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("User not found");
    }

    [Fact]
    public async Task GetSpreadsForUser_NoSpreadTypesEnabled_ReturnsEmptyList()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        using (var seed = CreateSharedContext(dbName))
        {
            seed.UsersSettings.Add(new UserSettingsModel { Id = 1, AccountId = "acc1" });
            seed.Users.Add(new AccountModel { Id = "acc1", UserSettingsId = 1, UserSettings = null! });
            await seed.SaveChangesAsync();
        }
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        var result = await _sut.GetSpreadsForUser("acc1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSpreadsForUser_FuturesEnabled_ReturnsDtosWithExchangeLinks()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        using (var seed = CreateSharedContext(dbName))
        {
            seed.UsersSettings.Add(new UserSettingsModel { Id = 2, AccountId = "acc2", FuturesSpread = true });
            seed.Users.Add(new AccountModel { Id = "acc2", UserSettingsId = 2, UserSettings = null! });
            await seed.SaveChangesAsync();
        }
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        var spread = MakeModel();
        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures)).ReturnsAsync(new List<TradeOpportunityModel>());
        _spreadRepo.Setup(r => r.GetByTypeAsync(SpreadType.Futures)).ReturnsAsync(new List<TradeOpportunityModel> { spread });
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadsForUser("acc2");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetRecommendedSpreads_NoActiveSubscription_ReturnsFail()
    {
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(false);

        var result = await _sut.GetRecommendedSpreads("u1");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetRecommendedSpreads_UserNotFound_ReturnsFail()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        var result = await _sut.GetRecommendedSpreads("missing");

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("User not found");
    }

    [Fact]
    public async Task GetRecommendedSpreads_NoSpreadTypesEnabled_ReturnsEmptyList()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        using (var seed = CreateSharedContext(dbName))
        {
            seed.UsersSettings.Add(new UserSettingsModel { Id = 3, AccountId = "acc3" });
            seed.Users.Add(new AccountModel { Id = "acc3", UserSettingsId = 3, UserSettings = null! });
            await seed.SaveChangesAsync();
        }
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        var result = await _sut.GetRecommendedSpreads("acc3");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecommendedSpreads_FiltersBelowOnePercentAndPicksTopTwoByLowestCost()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        await SeedEligibleUserAsync(dbName, 10, "acc10", s => s.FuturesSpread = true);
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        var belowThreshold = MakeEligibleModel();
        belowThreshold.Spread = 0.5; // < 1%, must be excluded

        var cheap = MakeEligibleModel();
        cheap.Spread = 2.0;
        cheap.ExchangeLong.SummarySlipage = 0.1;

        var expensive = MakeEligibleModel();
        expensive.Spread = 3.0;
        expensive.ExchangeLong.SummarySlipage = 0.5;

        var mostExpensive = MakeEligibleModel();
        mostExpensive.Spread = 5.0;
        mostExpensive.ExchangeLong.SummarySlipage = 1.0;

        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures))
            .ReturnsAsync([belowThreshold, cheap, expensive, mostExpensive]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetRecommendedSpreads("acc10");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(r => r.Details.PositionModel.Guid)
            .Should().Equal(cheap.Guid, expensive.Guid);
        result.Value.Should().OnlyContain(r => r.Category == SpreadType.Futures);
    }

    [Fact]
    public async Task GetRecommendedSpreads_TieBreaksByHigherAbsoluteSpread()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        await SeedEligibleUserAsync(dbName, 11, "acc11", s => s.FuturesSpread = true);
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        // All three have identical cost (zero slippage/funding) - only Spread differs.
        var low = MakeEligibleModel();
        low.Spread = 1.5;
        var mid = MakeEligibleModel();
        mid.Spread = 3.0;
        var high = MakeEligibleModel();
        high.Spread = 5.0;

        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures))
            .ReturnsAsync([low, mid, high]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetRecommendedSpreads("acc11");

        result.Value.Select(r => r.Details.PositionModel.Guid).Should().Equal(high.Guid, mid.Guid);
    }

    [Fact]
    public async Task GetRecommendedSpreads_FundingCategory_IgnoresFundingRateInRanking()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        await SeedEligibleUserAsync(dbName, 12, "acc12", s => s.FundingSpread = true);
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        var lowerSpreadHugeFunding = MakeEligibleModel();
        lowerSpreadHugeFunding.Type = SpreadType.Funding;
        lowerSpreadHugeFunding.Spread = 2.0;
        lowerSpreadHugeFunding.ExchangeLong.FundingRateValue = 0.5; // would dominate cost if not ignored for Funding type

        var higherSpreadNoFunding = MakeEligibleModel();
        higherSpreadNoFunding.Type = SpreadType.Funding;
        higherSpreadNoFunding.Spread = 4.0;

        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Funding))
            .ReturnsAsync([lowerSpreadHugeFunding, higherSpreadNoFunding]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetRecommendedSpreads("acc12");

        // Both have zero cost (funding ignored for Funding type, no slippage) - tie-break by
        // spread should still return both, but the huge FundingRateValue must not have pushed
        // lowerSpreadHugeFunding out of the ranking.
        result.Value.Should().HaveCount(2);
        result.Value.Select(r => r.Details.PositionModel.Guid)
            .Should().Contain(lowerSpreadHugeFunding.Guid);
    }

    [Fact]
    public async Task GetRecommendedSpreads_SpotCategory_OnlyCountsShortLegFundingRate()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        await SeedEligibleUserAsync(dbName, 13, "acc13", s => s.SpotSpread = true);
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);

        var longFundingIgnored = MakeEligibleModel();
        longFundingIgnored.Type = SpreadType.Spot;
        longFundingIgnored.Spread = 2.0;
        longFundingIgnored.ExchangeLong.FundingRateValue = 0.5; // spot leg - must be ignored

        var shortFundingCounted = MakeEligibleModel();
        shortFundingCounted.Type = SpreadType.Spot;
        shortFundingCounted.Spread = 2.0;
        shortFundingCounted.ExchangeShort.FundingRateValue = 0.01; // small but non-zero cost

        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Spot))
            .ReturnsAsync([longFundingIgnored, shortFundingCounted]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetRecommendedSpreads("acc13");

        // longFundingIgnored has zero effective cost (its funding is on the long/spot leg,
        // which never counts) so it ranks ahead of shortFundingCounted despite equal spread.
        result.Value.Select(r => r.Details.PositionModel.Guid).Should()
            .Equal(longFundingIgnored.Guid, shortFundingCounted.Guid);
    }

    [Fact]
    public async Task GetSpreadInfo_AboveMinSpreadAndWithinCostThreshold_AnalysisRecommendedTrue()
    {
        var guid = Guid.NewGuid();
        var model = MakeModel(guid);
        model.Spread = 5.0;
        model.SummaryTarrif = 0.05;
        var ticker = new TradeOpportunityTickerModel(model);

        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(model);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync(ticker);
        _tickerRepo.Setup(r => r.GetRemainingWithoutOrderBookAsync(guid, 1, 49))
            .ReturnsAsync([]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.Value.Analysis.Should().NotBeNull();
        result.Value.Analysis!.Recommended.Should().BeTrue();
        result.Value.Analysis.Reasons.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSpreadInfo_BelowMinSpread_AnalysisRecommendedFalse()
    {
        var guid = Guid.NewGuid();
        var model = MakeModel(guid);
        model.Spread = 0.4;
        var ticker = new TradeOpportunityTickerModel(model);

        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(model);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync(ticker);
        _tickerRepo.Setup(r => r.GetRemainingWithoutOrderBookAsync(guid, 1, 49))
            .ReturnsAsync([]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.Value.Analysis!.Recommended.Should().BeFalse();
        result.Value.Analysis.Reasons[0].Should().Contain("below the 1% minimum");
    }

    [Fact]
    public async Task GetSpreadInfo_CostExceedsTenPercentOfSpread_AnalysisRecommendedFalse()
    {
        var guid = Guid.NewGuid();
        var model = MakeModel(guid);
        model.Spread = 2.0;
        model.SummaryTarrif = 0.5; // 25% of spread, well above the 10% limit
        var ticker = new TradeOpportunityTickerModel(model);

        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(model);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync(ticker);
        _tickerRepo.Setup(r => r.GetRemainingWithoutOrderBookAsync(guid, 1, 49))
            .ReturnsAsync([]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.Value.Analysis!.Recommended.Should().BeFalse();
        result.Value.Analysis.Reasons[1].Should().Contain("above the 10% limit");
    }

    [Fact]
    public async Task GetSpreadInfo_FundingType_IgnoresFundingRateInCostCheck()
    {
        var guid = Guid.NewGuid();
        var model = MakeModel(guid);
        model.Type = SpreadType.Funding;
        model.Spread = 1.58;
        model.SummaryTarrif = 0.02; // fee + slippage well within the 10% limit on their own
        model.ExchangeLong.FundingRateValue = 0.02; // would push cost ratio well above the 10% limit if wrongly counted
        model.ExchangeShort.FundingRateValue = 0.02;
        var ticker = new TradeOpportunityTickerModel(model);

        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(model);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync(ticker);
        _tickerRepo.Setup(r => r.GetRemainingWithoutOrderBookAsync(guid, 1, 49))
            .ReturnsAsync([]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.Value.Analysis!.Recommended.Should().BeTrue();
        result.Value.Analysis.Reasons[1].Should().Be("Combined fee and slippage cost is 1% of the spread, within the 10% limit.");
    }

    [Fact]
    public async Task GetSpreadInfo_FundingTypeWithFastFallingTickerHistory_SetsTrendWarning()
    {
        var guid = Guid.NewGuid();
        var model = MakeModel(guid);
        model.Type = SpreadType.Funding;
        model.Spread = 2.0;
        var latest = new TradeOpportunityTickerModel(model) { Spread = 1.0 };
        var older = new List<TradeOpportunityTickerModel>
        {
            new(model) { Spread = 1.4 },
            new(model) { Spread = 2.0 }, // oldest: 1.0 is a 50% relative decline from 2.0
        };

        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(model);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync(latest);
        _tickerRepo.Setup(r => r.GetRemainingWithoutOrderBookAsync(guid, 1, 49)).ReturnsAsync(older);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.Value.Analysis!.TrendWarning.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSpreadInfo_NonFundingTypeWithFastFallingTickerHistory_NoTrendWarning()
    {
        var guid = Guid.NewGuid();
        var model = MakeModel(guid);
        model.Type = SpreadType.Futures;
        model.Spread = 2.0;
        var latest = new TradeOpportunityTickerModel(model) { Spread = 1.0 };
        var older = new List<TradeOpportunityTickerModel>
        {
            new(model) { Spread = 1.4 },
            new(model) { Spread = 2.0 },
        };

        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(model);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync(latest);
        _tickerRepo.Setup(r => r.GetRemainingWithoutOrderBookAsync(guid, 1, 49)).ReturnsAsync(older);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.Value.Analysis!.TrendWarning.Should().BeNull();
    }

    [Fact]
    public async Task GetSpreadInfo_FewerThanThreeTickers_NoTrendWarning()
    {
        var guid = Guid.NewGuid();
        var model = MakeModel(guid);
        model.Type = SpreadType.Funding;
        model.Spread = 2.0;
        var latest = new TradeOpportunityTickerModel(model) { Spread = 0.1 };

        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetByGuidAsync(guid)).ReturnsAsync(model);
        _tickerRepo.Setup(r => r.GetLatestByGuidAsync(guid)).ReturnsAsync(latest);
        _tickerRepo.Setup(r => r.GetRemainingWithoutOrderBookAsync(guid, 1, 49))
            .ReturnsAsync([new(model) { Spread = 2.0 }]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadInfo(guid.ToString());

        result.Value.Analysis!.TrendWarning.Should().BeNull();
    }

    [Fact]
    public async Task GetSpreadsForUser_LeavesAnalysisNull()
    {
        var dbName = Guid.NewGuid().ToString();
        SetupContextFactory(dbName);
        await SeedEligibleUserAsync(dbName, 20, "acc20", s => s.FuturesSpread = true);
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures)).ReturnsAsync([MakeEligibleModel()]);
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetSpreadsForUser("acc20");

        result.Value.Should().OnlyContain(dto => dto.Analysis == null);
    }
}
