using ArbiScannerWeb.Infrastructure.HealthChecks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure.HealthChecks;

public class DbContextFactoryHealthCheckTests
{
    public sealed class FakeDatabaseFacade(DbContext context, bool canConnect) : DatabaseFacade(context)
    {
        public override Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(canConnect);
    }

    public sealed class FakeDbContext(bool canConnect)
        : DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
    {
        public override DatabaseFacade Database => new FakeDatabaseFacade(this, canConnect);
    }

    private static Mock<IDbContextFactory<FakeDbContext>> CreateFactory(bool canConnect)
    {
        var factory = new Mock<IDbContextFactory<FakeDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FakeDbContext(canConnect));
        return factory;
    }

    [Fact]
    public async Task CheckHealthAsync_CanConnect_ReturnsHealthy()
    {
        var factory = CreateFactory(canConnect: true);
        var check = new DbContextFactoryHealthCheck<FakeDbContext>(factory.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_CannotConnect_ReturnsUnhealthy()
    {
        var factory = CreateFactory(canConnect: false);
        var check = new DbContextFactoryHealthCheck<FakeDbContext>(factory.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
