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
}
