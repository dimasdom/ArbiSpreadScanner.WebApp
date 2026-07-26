using ArbiScannerWeb.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class TradeOpportunityHubTests
{
    private readonly Mock<ILogger<TradeOpportunityHub>> _logger = new();
    private readonly Mock<HubCallerContext> _context = new();
    private readonly Mock<IGroupManager> _groups = new();
    private readonly TradeOpportunityHub _sut;

    public TradeOpportunityHubTests()
    {
        _sut = new TradeOpportunityHub(_logger.Object)
        {
            Context = _context.Object,
            Groups = _groups.Object
        };
        _context.Setup(c => c.ConnectionId).Returns("conn-1");
    }

    [Fact]
    public async Task OnConnectedAsync_LogsConnection()
    {
        await _sut.OnConnectedAsync();

        _logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_LogsDisconnection()
    {
        await _sut.OnDisconnectedAsync(null);

        _logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinGroup_AddsConnectionToGroup()
    {
        await _sut.JoinGroup("group-a");

        _groups.Verify(g => g.AddToGroupAsync("conn-1", "group-a", default), Times.Once);
    }

    [Fact]
    public async Task LeaveGroup_RemovesConnectionFromGroup()
    {
        await _sut.LeaveGroup("group-a");

        _groups.Verify(g => g.RemoveFromGroupAsync("conn-1", "group-a", default), Times.Once);
    }
}
