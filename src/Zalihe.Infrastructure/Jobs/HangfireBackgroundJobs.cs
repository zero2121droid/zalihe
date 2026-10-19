using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zalihe.Application.Alerts;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Application.Webhooks;
using Zalihe.Infrastructure.Tenancy;

namespace Zalihe.Infrastructure.Jobs;

/// <summary>Schedules jobs in Hangfire, which stores them in PostgreSQL (schema "hangfire") and retries failures.</summary>
public class HangfireBackgroundJobs(IBackgroundJobClient client, IConfiguration configuration) : IBackgroundJobs
{
    public void ProcessWebhookEvent(Guid tenantId, Guid webhookEventId) =>
        client.Enqueue<WebhookEventJob>(job => job.RunAsync(tenantId, webhookEventId, CancellationToken.None));

    /// <summary>
    /// After "Jobs:StockPushDelaySeconds" (30 in appsettings.json). Every change schedules its own job;
    /// the first one sends everything changed so far and the rest find little or nothing left. Empty
    /// turns pushing off (tests do, and call the push directly).
    /// </summary>
    public void PushStock(Guid tenantId, Guid channelId)
    {
        if (!int.TryParse(configuration["Jobs:StockPushDelaySeconds"], out var seconds)) return;
        client.Schedule<StockPushJob>(job => job.RunAsync(tenantId, channelId, CancellationToken.None), TimeSpan.FromSeconds(seconds));
    }

    public void EmailChannelFailure(Guid tenantId, Guid channelId) =>
        client.Enqueue<ChannelFailureEmailJob>(job => job.RunAsync(tenantId, channelId, CancellationToken.None));
}

/// <summary>
/// Emails that a shop stopped working. A mail server that is down fails the job and Hangfire tries
/// again with growing pauses; the channel remembers a sent email, so a retry never sends it twice.
/// </summary>
public class ChannelFailureEmailJob(TenantContext tenant, ChannelFailureAlertService alerts)
{
    public async Task RunAsync(Guid tenantId, Guid channelId, CancellationToken ct)
    {
        tenant.Set(tenantId);
        await alerts.SendAsync(channelId, ct);
    }
}

/// <summary>
/// Sends a shop's changed stock. A failure is retried by Hangfire; a later change schedules its own push
/// anyway, and every push sends everything still unsent.
/// </summary>
public class StockPushJob(TenantContext tenant, StockPushService stockPush, ILogger<StockPushJob> logger)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task RunAsync(Guid tenantId, Guid channelId, CancellationToken ct)
    {
        tenant.Set(tenantId);
        var result = await stockPush.PushAsync(channelId, ct);
        if (result.Rejected > 0)
        {
            logger.LogWarning("Shop {ChannelId} rejected the stock of {Rejected} products", channelId, result.Rejected);
        }
    }
}

/// <summary>
/// Applies one saved webhook event to stock. Hangfire runs every job in its own DI scope, so the
/// company is set for this job only. A failure is recorded on the event and thrown again, so
/// Hangfire retries it later with growing pauses.
/// </summary>
public class WebhookEventJob(TenantContext tenant, WebhookService webhooks, IServiceScopeFactory scopes)
{
    public async Task RunAsync(Guid tenantId, Guid webhookEventId, CancellationToken ct)
    {
        tenant.Set(tenantId);
        try
        {
            await webhooks.ProcessAsync(webhookEventId, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // A fresh scope: the failed one may still track the half-done changes that failed.
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
            await scope.ServiceProvider.GetRequiredService<WebhookService>()
                .RecordFailureAsync(webhookEventId, $"{e.GetType().Name}: {e.Message}", ct);
            throw;
        }
    }
}
