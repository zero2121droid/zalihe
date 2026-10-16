using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Zalihe.Application.Common;
using Zalihe.Application.Webhooks;
using Zalihe.Infrastructure.Tenancy;

namespace Zalihe.Infrastructure.Jobs;

/// <summary>Schedules jobs in Hangfire, which stores them in PostgreSQL (schema "hangfire") and retries failures.</summary>
public class HangfireBackgroundJobs(IBackgroundJobClient client) : IBackgroundJobs
{
    public void ProcessWebhookEvent(Guid tenantId, Guid webhookEventId) =>
        client.Enqueue<WebhookEventJob>(job => job.RunAsync(tenantId, webhookEventId, CancellationToken.None));
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
