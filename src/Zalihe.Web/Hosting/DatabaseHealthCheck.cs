using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Web.Hosting;

/// <summary>
/// /health answers 200 only while the database is reachable, so an uptime monitor notices both a
/// stopped app and a stopped database.
/// </summary>
public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database can't be reached.");
}
