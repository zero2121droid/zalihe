using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;

namespace Zalihe.Application.Orders;

/// <param name="OrderCount">Orders the shop reported as changed; most were usually applied already by webhooks.</param>
/// <param name="MovementCount">Stock movements recorded, i.e. what webhooks had missed.</param>
public record OrderSyncResult(int OrderCount, int MovementCount);

/// <summary>
/// Periodic sync (SPEC 7.4): asks the shop for the orders changed since the last sync and applies them
/// through <see cref="OrderService"/>, like webhook events. A shop doesn't resend a webhook it failed
/// to deliver, so this is what catches orders placed while the application was down or unreachable.
/// Called by a background job with the shop's company set.
/// </summary>
public class OrderSyncService(
    IAppDbContext db,
    ICredentialProtector protector,
    ISalesChannelFactory channels,
    OrderService orders,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How far back each sync reaches before the previous one started. Covers a shop clock that runs
    /// behind ours and orders saved just as the previous sync read; applying an order again changes nothing.
    /// </summary>
    public static readonly TimeSpan Overlap = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Applies the orders changed since the last sync. Throws <see cref="SalesChannelException"/> when the
    /// shop can't be read; the failure is recorded on the channel (SPEC 7.6) and the last sync time stays,
    /// so the next run asks for the same orders again.
    /// </summary>
    public async Task<OrderSyncResult> SyncAsync(Guid channelId, CancellationToken ct)
    {
        var channel = await db.SalesChannels.SingleOrDefaultAsync(c => c.Id == channelId, ct);
        if (channel is null)
        {
            return new OrderSyncResult(0, 0); // removed after the job was scheduled
        }

        var startedAt = timeProvider.GetUtcNow();
        // The first sync starts at connecting: older orders are already in the stock taken over at import.
        var since = channel.LastSyncedAt is { } last ? last - Overlap : channel.CreatedAt;
        IReadOnlyList<ShopOrder> changed;
        try
        {
            changed = await channels.Create(channel, protector).FetchOrdersModifiedSinceAsync(since, ct);
        }
        catch (SalesChannelException e)
        {
            channel.RecordFailure(e.Code, timeProvider.GetUtcNow());
            await db.SaveChangesAsync(ct);
            throw;
        }

        channel.MarkConnected();

        var movements = 0;
        foreach (var order in changed)
        {
            movements += (await orders.ProcessAsync(channel.Id, order, ct)).MovementCount;
        }

        channel.MarkSynced(startedAt);
        await db.SaveChangesAsync(ct);
        return new OrderSyncResult(changed.Count, movements);
    }
}
