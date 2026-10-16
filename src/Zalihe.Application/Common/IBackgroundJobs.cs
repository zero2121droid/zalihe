namespace Zalihe.Application.Common;

/// <summary>
/// Work that runs after the request has been answered. Each job gets the company explicitly,
/// because there is no signed-in user to take it from (SPEC 3.3). Failed jobs are retried.
/// </summary>
public interface IBackgroundJobs
{
    void ProcessWebhookEvent(Guid tenantId, Guid webhookEventId);

    /// <summary>
    /// Sends the changed stock of a shop's items to the shop after a short pause, so changes made
    /// together (an order with several lines, a quick series of receipts) go in one request.
    /// </summary>
    void PushStock(Guid tenantId, Guid channelId);
}
