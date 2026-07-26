using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Services;
using FluentResults;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class StaleSpreadCleanupJobTests
{
    private readonly Mock<ITradeOpportunityRepository> _spreadRepo = new();
    private readonly Mock<ITradeOpportunityService> _spreadService = new();
    private readonly Mock<ITradeOpportunityTickerRepository> _tickerRepo = new();
    private readonly Mock<ILogger<StaleSpreadCleanupJob>> _logger = new();
    private readonly Mock<IJobCancellationToken> _cancellationToken = new();
    private readonly StaleSpreadCleanupJob _sut;

    public StaleSpreadCleanupJobTests()
    {
        _sut = new StaleSpreadCleanupJob(_spreadRepo.Object, _spreadService.Object, _tickerRepo.Object, _logger.Object);
        foreach (var type in Enum.GetValues<SpreadType>())
            _spreadRepo.Setup(r => r.GetOpenByTypeAsync(type)).ReturnsAsync(new List<TradeOpportunityModel>());
        _tickerRepo.Setup(r => r.DeleteOrphanedAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(0);
    }

    private static TradeOpportunityModel BuildSpread(Guid guid, DateTime dateTime) => new()
    {
        Guid = guid,
        DateTime = dateTime,
        Symbol = "BTC/USDT",
        ExchangeRateA = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "binance" },
        ExchangeRateB = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "okx" },
        ExchangeShort = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "binance" },
        ExchangeLong = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "okx" }
    };

    [Fact]
    public async Task ExecuteAsync_NoOpenSpreads_DoesNothing()
    {
        await _sut.ExecuteAsync(_cancellationToken.Object);

        _spreadService.Verify(s => s.CloseSpread(It.IsAny<TradeOpportunityModel>()), Times.Never);
        _tickerRepo.Verify(r => r.DeleteOrphanedAsync(It.IsAny<IEnumerable<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FreshSpread_NotClosed()
    {
        var fresh = BuildSpread(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-10));
        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures)).ReturnsAsync(new List<TradeOpportunityModel> { fresh });

        await _sut.ExecuteAsync(_cancellationToken.Object);

        _spreadService.Verify(s => s.CloseSpread(It.IsAny<TradeOpportunityModel>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StaleSpread_ClosedSuccessfully_LogsInfo()
    {
        var stale = BuildSpread(Guid.NewGuid(), DateTime.UtcNow.AddHours(-5));
        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures)).ReturnsAsync(new List<TradeOpportunityModel> { stale });
        _spreadService.Setup(s => s.CloseSpread(stale)).ReturnsAsync(Result.Ok());

        await _sut.ExecuteAsync(_cancellationToken.Object);

        _spreadService.Verify(s => s.CloseSpread(stale), Times.Once);
        _tickerRepo.Verify(r => r.DeleteOrphanedAsync(It.Is<IEnumerable<Guid>>(g => !g.Contains(stale.Guid))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StaleSpread_CloseFails_LogsError()
    {
        var stale = BuildSpread(Guid.NewGuid(), DateTime.UtcNow.AddHours(-5));
        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures)).ReturnsAsync(new List<TradeOpportunityModel> { stale });
        _spreadService.Setup(s => s.CloseSpread(stale)).ReturnsAsync(Result.Fail("boom"));

        await _sut.ExecuteAsync(_cancellationToken.Object);

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OrphanedTickersDeleted_LogsInfo()
    {
        _tickerRepo.Setup(r => r.DeleteOrphanedAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(3);

        await _sut.ExecuteAsync(_cancellationToken.Object);

        _logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("orphaned")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_PropagatesException()
    {
        var stale = BuildSpread(Guid.NewGuid(), DateTime.UtcNow.AddHours(-5));
        _spreadRepo.Setup(r => r.GetOpenByTypeAsync(SpreadType.Futures)).ReturnsAsync(new List<TradeOpportunityModel> { stale });
        _cancellationToken.Setup(c => c.ThrowIfCancellationRequested()).Throws<OperationCanceledException>();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ExecuteAsync(_cancellationToken.Object));

        _spreadService.Verify(s => s.CloseSpread(It.IsAny<TradeOpportunityModel>()), Times.Never);
    }
}
