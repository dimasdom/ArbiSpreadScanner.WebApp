using ArbiScannerWeb.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class ClientLogControllerTests
{
    private readonly Mock<ILogger<ClientLogController>> _logger = new();
    private readonly ClientLogController _sut;

    public ClientLogControllerTests()
    {
        _sut = new ClientLogController(_logger.Object);
        _logger.Setup(l => l.BeginScope(It.IsAny<Dictionary<string, object>>()))
            .Returns(Mock.Of<IDisposable>());
    }

    private void VerifyLoggedAt(LogLevel level, Times times) =>
        _logger.Verify(l => l.Log(
            level,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    [Theory]
    [InlineData("error", LogLevel.Error)]
    [InlineData("warn", LogLevel.Warning)]
    [InlineData("warning", LogLevel.Warning)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("info", LogLevel.Information)]
    [InlineData("something-else", LogLevel.Information)]
    public void Log_MapsLevelStringToLogLevel(string level, LogLevel expected)
    {
        var entries = new List<FrontendLogEntry>
        {
            new(level, "message", "source", DateTimeOffset.UtcNow, "details")
        };

        var result = _sut.Log(entries);

        result.Should().BeOfType<OkResult>();
        VerifyLoggedAt(expected, Times.Once());
    }

    [Fact]
    public void Log_MissingOptionalFields_UsesDefaults()
    {
        var entries = new List<FrontendLogEntry> { new("ERROR", "msg", null, null, null) };

        var result = _sut.Log(entries);

        result.Should().BeOfType<OkResult>();
        VerifyLoggedAt(LogLevel.Error, Times.Once());
    }

    [Fact]
    public void Log_MultipleEntries_LogsEachOnce()
    {
        var entries = new List<FrontendLogEntry>
        {
            new("error", "m1", "s1", DateTimeOffset.UtcNow, "d1"),
            new("debug", "m2", "s2", DateTimeOffset.UtcNow, "d2")
        };

        _sut.Log(entries);

        VerifyLoggedAt(LogLevel.Error, Times.Once());
        VerifyLoggedAt(LogLevel.Debug, Times.Once());
    }

    [Fact]
    public void Log_EmptyEntries_ReturnsOkWithoutLogging()
    {
        var result = _sut.Log([]);

        result.Should().BeOfType<OkResult>();
        VerifyLoggedAt(LogLevel.Error, Times.Never());
        VerifyLoggedAt(LogLevel.Warning, Times.Never());
        VerifyLoggedAt(LogLevel.Debug, Times.Never());
        VerifyLoggedAt(LogLevel.Information, Times.Never());
    }
}
