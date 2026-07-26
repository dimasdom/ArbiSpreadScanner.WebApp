using System.Reflection;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Services;
using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class MessageProcessingServiceTests
{
    private readonly Mock<IRabbitMqService> _rabbitMqService = new();
    private readonly Mock<ITradeOpportunityService> _spreadService = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<ILogger<MessageProcessingService>> _logger = new();
    private readonly MessageProcessingService _sut;

    public MessageProcessingServiceTests()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(ITradeOpportunityService))).Returns(_spreadService.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _sut = new MessageProcessingService(_rabbitMqService.Object, _scopeFactory.Object, _logger.Object);
    }

    private static Task InvokeExecuteAsync(MessageProcessingService service, CancellationToken token)
    {
        var method = typeof(MessageProcessingService).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(service, [token])!;
    }

    private static Task InvokeProcessMessageAsync(MessageProcessingService service, TradeOpportunityModel model)
    {
        var method = typeof(MessageProcessingService).GetMethod("ProcessMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(service, [model])!;
    }

    private static TradeOpportunityModel BuildModel(MarketPositionAction action) => new()
    {
        Guid = Guid.NewGuid(),
        Symbol = "BTC/USDT",
        ActionType = action,
        ExchangeRateA = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "binance" },
        ExchangeRateB = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "okx" },
        ExchangeShort = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "binance" },
        ExchangeLong = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "okx" }
    };

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelled_StopsConsumingAndLogs()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _rabbitMqService.Setup(r => r.StopConsumingAsync()).Returns(Task.CompletedTask);

        await InvokeExecuteAsync(_sut, cts.Token);

        _rabbitMqService.Verify(r => r.StartConsumingAsync(It.IsAny<CancellationToken>()), Times.Never);
        _rabbitMqService.Verify(r => r.StopConsumingAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FinalStopConsumingThrows_LogsError()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _rabbitMqService.Setup(r => r.StopConsumingAsync()).ThrowsAsync(new InvalidOperationException("channel closed"));

        await InvokeExecuteAsync(_sut, cts.Token);

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StartConsumingThrows_StopsAndRetries()
    {
        using var cts = new CancellationTokenSource();
        _rabbitMqService.Setup(r => r.StartConsumingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connect failed"));
        _rabbitMqService.Setup(r => r.StopConsumingAsync())
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InvokeExecuteAsync(_sut, cts.Token));

        _rabbitMqService.Verify(r => r.StopConsumingAsync(), Times.Once);
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_OpenAction_CallsAddSpread()
    {
        var model = BuildModel(MarketPositionAction.Open);
        _spreadService.Setup(s => s.AddSpread(model)).ReturnsAsync(Result.Ok());

        await InvokeProcessMessageAsync(_sut, model);

        _spreadService.Verify(s => s.AddSpread(model), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_UpdateAction_CallsUpdateSpread()
    {
        var model = BuildModel(MarketPositionAction.Update);
        _spreadService.Setup(s => s.UpdateSpread(model)).ReturnsAsync(Result.Ok());

        await InvokeProcessMessageAsync(_sut, model);

        _spreadService.Verify(s => s.UpdateSpread(model), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_CloseAction_CallsCloseSpread()
    {
        var model = BuildModel(MarketPositionAction.Close);
        _spreadService.Setup(s => s.CloseSpread(model)).ReturnsAsync(Result.Ok());

        await InvokeProcessMessageAsync(_sut, model);

        _spreadService.Verify(s => s.CloseSpread(model), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_UnknownAction_LogsWarningWithoutCallingService()
    {
        var model = BuildModel((MarketPositionAction)999);

        await InvokeProcessMessageAsync(_sut, model);

        _spreadService.Verify(s => s.AddSpread(It.IsAny<TradeOpportunityModel>()), Times.Never);
        _spreadService.Verify(s => s.UpdateSpread(It.IsAny<TradeOpportunityModel>()), Times.Never);
        _spreadService.Verify(s => s.CloseSpread(It.IsAny<TradeOpportunityModel>()), Times.Never);
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
