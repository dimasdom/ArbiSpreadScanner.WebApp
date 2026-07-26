using ArbiScannerWeb.Infrastructure.HealthChecks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArbiScannerWeb.Tests.Infrastructure.HealthChecks;

public class DbContextHealthCheckTests
{
    private sealed class FakeDatabaseFacade(DbContext context, bool canConnect) : DatabaseFacade(context)
    {
        public override Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(canConnect);
    }

    private sealed class FakeDbContext(bool canConnect)
        : DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
    {
        public override DatabaseFacade Database => new FakeDatabaseFacade(this, canConnect);
    }

    [Fact]
    public async Task CheckHealthAsync_CanConnect_ReturnsHealthy()
    {
        var check = new DbContextHealthCheck<FakeDbContext>(new FakeDbContext(canConnect: true));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_CannotConnect_ReturnsUnhealthy()
    {
        var check = new DbContextHealthCheck<FakeDbContext>(new FakeDbContext(canConnect: false));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
