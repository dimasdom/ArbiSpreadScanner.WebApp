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
            .ReturnsAsync(new List<TradeOpportunityTickerModel>());
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExchangeLinkModel>());

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
        _linkRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExchangeLinkModel>());

        var result = await _sut.GetSpreadsForUser("acc2");

        result.IsSuccess.Should().BeTrue();
    }
}
