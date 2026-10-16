using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Channels;

/// <summary>
/// A webhook delivery from a shop (the inbox, SPEC 7.3). It is saved before anything else happens
/// and processed in the background, so the shop gets its answer at once and a failed processing
/// can be retried from what was saved. The payload holds only what stock needs: never customer data.
/// </summary>
public class WebhookEvent : ITenantOwned
{
    public const int TopicMaxLength = 64;
    public const int ErrorMaxLength = 1000;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ChannelId { get; private set; }

    /// <summary>The shop's event name, e.g. "order.updated".</summary>
    public string Topic { get; private set; }

    /// <summary>JSON with the order's number, status, time and lines, read from the delivery.</summary>
    public string Payload { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>When the event was applied to stock; null while it waits or keeps failing.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Why the last processing attempt failed; null once processed.</summary>
    public string? Error { get; private set; }

    public WebhookEvent(SalesChannel channel, string topic, string payload, DateTimeOffset receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(topic.Length, TopicMaxLength, nameof(topic));
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        Id = Guid.CreateVersion7();
        TenantId = channel.TenantId;
        ChannelId = channel.Id;
        Topic = topic;
        Payload = payload;
        ReceivedAt = receivedAt;
    }

    // For EF Core.
    private WebhookEvent()
    {
        Topic = null!;
        Payload = null!;
    }

    public bool IsProcessed => ProcessedAt is not null;

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Error = error.Length <= ErrorMaxLength ? error : error[..ErrorMaxLength];
    }
}
