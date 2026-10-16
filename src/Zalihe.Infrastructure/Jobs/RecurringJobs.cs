using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zalihe.Application.Common;
using Zalihe.Application.Orders;
using Zalihe.Application.Webhooks;
using Zalihe.Infrastructure.Tenancy;

namespace Zalihe.Infrastructure.Jobs;

/// <summary>
/// Jobs that run on a schedule. The schedules are cron expressions from configuration ("Jobs" section);
/// an empty one turns the job off (tests do, so no job runs in the middle of one).
/// </summary>
public static class RecurringJobs
{
    public const string OrderSyncId = "order-sync";
    public const string WebhookCleanupId = "webhook-event-cleanup";

    /// <summary>Registers (or updates) the jobs in Hangfire's storage; called once at startup.</summary>
    public static void ScheduleRecurringJobs(this IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var manager = services.GetRequiredService<IRecurringJobManager>();

        Schedule<OrderSyncJob>(manager, OrderSyncId, configuration["Jobs:OrderSyncCron"],
            job => job.ScheduleAllAsync(CancellationToken.None));
        Schedule<WebhookCleanupJob>(manager, WebhookCleanupId, configuration["Jobs:WebhookCleanupCron"],
            job => job.RunAsync(CancellationToken.None));
    }

    private static void Schedule<TJob>(IRecurringJobManager manager, string id, string? cron,
        System.Linq.Expressions.Expression<Func<TJob, Task>> run)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            manager.RemoveIfExists(id);
            return;
        }

        manager.AddOrUpdate(id, run, cron, new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}

/// <summary>
/// The periodic order sync (SPEC 7.4): one job per shop, each with its company set, so a slow or
/// broken shop doesn't hold up the others. Failed runs aren't retried; the next scheduled run is the
/// retry, and it reaches back to the last successful one.
/// </summary>
public class OrderSyncJob(IAppDbContext db, IBackgroundJobClient jobs, TenantContext tenant, OrderSyncService sync,
    ILogger<OrderSyncJob> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task ScheduleAllAsync(CancellationToken ct)
    {
        foreach (var channel in await db.ListAllChannelsAsync(ct))
        {
            jobs.Enqueue<OrderSyncJob>(job => job.SyncChannelAsync(channel.TenantId, channel.ChannelId, CancellationToken.None));
        }
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task SyncChannelAsync(Guid tenantId, Guid channelId, CancellationToken ct)
    {
        tenant.Set(tenantId);
        var result = await sync.SyncAsync(channelId, ct);
        if (result.MovementCount > 0)
        {
            // Worth knowing: it means webhooks missed orders.
            logger.LogInformation("Order sync for channel {ChannelId} applied {MovementCount} movements missed by webhooks",
                channelId, result.MovementCount);
        }
    }
}

/// <summary>Deletes webhook events older than <see cref="WebhookService.Retention"/> (SPEC 8).</summary>
public class WebhookCleanupJob(WebhookService webhooks)
{
    public Task RunAsync(CancellationToken ct) => webhooks.DeleteExpiredAsync(ct);
}
