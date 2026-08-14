using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Services;
using FluentResults;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class SpreadStatsAggregationJobTests
{
    private readonly Mock<ISpreadStatsService> _statsService = new();
    private readonly Mock<ILogger<SpreadStatsAggregationJob>> _logger = new();
    private readonly Mock<IJobCancellationToken> _cancellationToken = new();
    private readonly SpreadStatsAggregationJob _sut;

    public SpreadStatsAggregationJobTests()
    {
        _sut = new SpreadStatsAggregationJob(_statsService.Object, _logger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceSucceeds_LogsInfo()
    {
        var snapshot = new SpreadStatsSnapshotModel
        {
            Id = Guid.NewGuid(),
            GeneratedAtUtc = DateTime.UtcNow,
            TotalSpreadsAnalyzed = 3,
            TopSymbolsByAverageSpread = [],
            TopExchangesByCount = [],
            TopExchangePairsByCount = [],
            MedianVolumeByExchange = [],
            SpreadTypeDistribution = [],
            TopSymbolsByCount = [],
        };
        _statsService.Setup(s => s.GenerateAndPersistSnapshotAsync()).ReturnsAsync(Result.Ok(snapshot));

        await _sut.ExecuteAsync(_cancellationToken.Object);

        _statsService.Verify(s => s.GenerateAndPersistSnapshotAsync(), Times.Once);
        _logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated spread stats snapshot")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceFails_LogsError()
    {
        _statsService.Setup(s => s.GenerateAndPersistSnapshotAsync())
            .ReturnsAsync(Result.Fail<SpreadStatsSnapshotModel>("boom"));

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
    public async Task ExecuteAsync_CancellationRequested_PropagatesException()
    {
        _cancellationToken.Setup(c => c.ThrowIfCancellationRequested()).Throws<OperationCanceledException>();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ExecuteAsync(_cancellationToken.Object));

        _statsService.Verify(s => s.GenerateAndPersistSnapshotAsync(), Times.Never);
    }
}
