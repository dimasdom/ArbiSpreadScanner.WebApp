using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArbiScannerWeb.Infrastructure.HealthChecks;

public class DbContextFactoryHealthCheck<TContext>(IDbContextFactory<TContext> dbContextFactory) : IHealthCheck
    where TContext : Microsoft.EntityFrameworkCore.DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
