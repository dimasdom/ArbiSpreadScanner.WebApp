using ArbiScannerWeb.API.Hubs;
using ArbiScannerWeb.API.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class SignalRServiceTests
{
    private readonly Mock<IHubContext<TradeOpportunityHub>> _hubContext = new();
    private readonly Mock<IHubClients> _hubClients = new();
    private readonly Mock<IClientProxy> _clientProxy = new();
    private readonly Mock<ILogger<SignalRService>> _logger = new();
    private readonly SignalRService _sut;

    public SignalRServiceTests()
    {
        _hubContext.Setup(h => h.Clients).Returns(_hubClients.Object);
        _hubClients.Setup(c => c.Group("group-a")).Returns(_clientProxy.Object);
        _sut = new SignalRService(_hubContext.Object, _logger.Object);
    }

    [Fact]
    public async Task NotifyGroupAsync_Success_LogsInformation()
    {
        _clientProxy.Setup(p => p.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.NotifyGroupAsync("group-a", new { message = "hi" });

        _clientProxy.Verify(p => p.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyGroupAsync_SendThrows_LogsErrorInsteadOfThrowing()
    {
        _clientProxy.Setup(p => p.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));

        await _sut.NotifyGroupAsync("group-a", new { message = "hi" });

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
