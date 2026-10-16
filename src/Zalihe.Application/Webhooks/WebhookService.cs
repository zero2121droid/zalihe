using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Application.Orders;
using Zalihe.Domain.Channels;

namespace Zalihe.Application.Webhooks;

public enum WebhookOutcome
{
    /// <summary>Saved and scheduled for processing.</summary>
    Accepted,

    /// <summary>Correctly signed, but not an order event; answered with success so the shop keeps the webhook.</summary>
    Ignored,

    InvalidSignature,
    NotFound,
}

/// <summary>
/// Webhooks from shops (SPEC 7.3). Receiving only checks the signature, saves the order and schedules
/// a job, so the shop gets its answer at once; the job applies the order to stock. The current
/// company must be set to the shop's company before either is called.
/// </summary>
public class WebhookService(
    IAppDbContext db,
    ICredentialProtector protector,
    IShopOrderReader orders,
    IBackgroundJobs jobs,
    OrderService orderService,
    TimeProvider timeProvider)
{
    public static readonly IReadOnlySet<string> OrderTopics = new HashSet<string>(StringComparer.Ordinal)
    {
        "order.created", "order.updated",
    };

    public async Task<WebhookOutcome> ReceiveAsync(Guid channelId, string? topic, string? signature, byte[] body, CancellationToken ct)
    {
        var channel = await db.SalesChannels.AsNoTracking().SingleOrDefaultAsync(c => c.Id == channelId, ct);
        if (channel is null)
        {
            return WebhookOutcome.NotFound;
        }

        if (!IsSignedBy(protector.Unprotect(channel.EncryptedWebhookSecret), body, signature))
        {
            return WebhookOutcome.InvalidSignature;
        }

        if (topic is null || !OrderTopics.Contains(topic) || orders.ReadWebhook(channel.Type, body) is not { } order)
        {
            return WebhookOutcome.Ignored;
        }

        // Only the order as read is stored, never the delivery itself, which holds the customer's data.
        var webhookEvent = new WebhookEvent(channel, topic, JsonSerializer.Serialize(order), timeProvider.GetUtcNow());
        db.WebhookEvents.Add(webhookEvent);
        await db.SaveChangesAsync(ct);

        // If scheduling fails, the event stays saved and unprocessed, and the periodic sync covers the order.
        jobs.ProcessWebhookEvent(channel.TenantId, webhookEvent.Id);
        return WebhookOutcome.Accepted;
    }

    /// <summary>Applies a saved event to stock. Does nothing for an event that is already processed.</summary>
    public async Task ProcessAsync(Guid webhookEventId, CancellationToken ct)
    {
        var webhookEvent = await db.WebhookEvents.SingleOrDefaultAsync(e => e.Id == webhookEventId, ct);
        if (webhookEvent is null || webhookEvent.IsProcessed)
        {
            return;
        }

        var order = JsonSerializer.Deserialize<ShopOrder>(webhookEvent.Payload)
            ?? throw new InvalidOperationException($"Webhook event {webhookEventId} has no order.");
        await orderService.ProcessAsync(webhookEvent.ChannelId, order, ct);

        webhookEvent.MarkProcessed(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Records why processing failed; called from a fresh scope, since the failed one may hold half-done changes.</summary>
    public async Task RecordFailureAsync(Guid webhookEventId, string error, CancellationToken ct)
    {
        var webhookEvent = await db.WebhookEvents.SingleOrDefaultAsync(e => e.Id == webhookEventId, ct);
        if (webhookEvent is null) return;

        webhookEvent.MarkFailed(error);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// WooCommerce signs the exact body: base64(HMAC-SHA256(body, secret)). Compared in constant time,
    /// so the answer time says nothing about how close a forged signature was.
    /// </summary>
    public static bool IsSignedBy(string secret, byte[] body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;

        byte[] received;
        try
        {
            received = Convert.FromBase64String(signature.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }
}
