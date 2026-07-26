using ArbiScannerWeb.Infrastructure.HealthChecks;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Tests.Infrastructure.HealthChecks;

public class RabbitMqHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_BrokerUnreachable_ReturnsUnhealthy()
    {
        var settings = Options.Create(new RabbitMqSettings
        {
            Host = "127.0.0.1",
            Port = 1,
            Username = "guest",
            Password = "guest",
            Queue = "q",
            Exchange = "e",
            RoutingKey = "rk"
        });
        var check = new RabbitMqHealthCheck(settings);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().NotBeNull();
    }
}
