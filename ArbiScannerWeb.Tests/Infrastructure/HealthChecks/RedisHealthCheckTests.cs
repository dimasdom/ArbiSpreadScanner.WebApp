using ArbiScannerWeb.Infrastructure.HealthChecks;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Infrastructure.HealthChecks;

public class RedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_PingSucceeds_ReturnsHealthy()
    {
        var (multiplexer, database) = MockHelpers.CreateRedisMocks();
        database.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.FromMilliseconds(5));
        var check = new RedisHealthCheck(multiplexer.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_PingThrows_ReturnsUnhealthy()
    {
        var (multiplexer, database) = MockHelpers.CreateRedisMocks();
        database.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
        var check = new RedisHealthCheck(multiplexer.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeOfType<RedisConnectionException>();
    }
}
