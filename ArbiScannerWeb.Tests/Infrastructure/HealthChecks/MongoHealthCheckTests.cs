using ArbiScannerWeb.Infrastructure.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ArbiScannerWeb.Tests.Infrastructure.HealthChecks;

public class MongoHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_PingSucceeds_ReturnsHealthy()
    {
        var database = new Mock<IMongoDatabase>();
        database.Setup(d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(), It.IsAny<ReadPreference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument());
        var client = new Mock<IMongoClient>();
        client.Setup(c => c.GetDatabase("admin", null)).Returns(database.Object);
        var check = new MongoHealthCheck(client.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_PingThrows_ReturnsUnhealthy()
    {
        var database = new Mock<IMongoDatabase>();
        database.Setup(d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(), It.IsAny<ReadPreference>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("mongo unreachable"));
        var client = new Mock<IMongoClient>();
        client.Setup(c => c.GetDatabase("admin", null)).Returns(database.Object);
        var check = new MongoHealthCheck(client.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeOfType<TimeoutException>();
    }
}
