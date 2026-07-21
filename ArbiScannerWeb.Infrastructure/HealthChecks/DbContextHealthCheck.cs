using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArbiScannerWeb.Infrastructure.HealthChecks;

public class DbContextHealthCheck<TContext>(TContext dbContext) : IHealthCheck where TContext : Microsoft.EntityFrameworkCore.DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
