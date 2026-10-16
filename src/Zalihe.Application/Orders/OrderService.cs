using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Domain.Orders;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Orders;

/// <param name="Ignored">The event was older than one already processed, so nothing changed.</param>
/// <param name="MovementCount">Stock movements recorded (sales and returns).</param>
/// <param name="UnlinkedLineCount">Order lines whose product isn't linked to an item and took no stock.</param>
public record ProcessOrderResult(bool Ignored, int MovementCount, int UnlinkedLineCount);

/// <summary>
/// Applies orders from a shop to stock (SPEC 6). Called for webhook events and by the periodic sync,
/// with the tenant already set; the same order may arrive any number of times, in any order.
/// </summary>
public class OrderService(IAppDbContext db, TimeProvider timeProvider)
{
    /// <summary>
    /// Brings the stored order to the state the shop reports and records a sale or return for every
    /// difference, all in one transaction.
    /// </summary>
    public async Task<ProcessOrderResult> ProcessAsync(Guid channelId, ShopOrder order, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var channel = await db.SalesChannels.SingleOrDefaultAsync(c => c.Id == channelId, ct)
            ?? throw new InvalidOperationException($"Sales channel {channelId} was not found for the current company.");

        // Waits while another event for this order is being processed, then reads what it applied.
        var stored = await db.LockOrAddExternalOrderAsync(new ExternalOrder(channel, order.ExternalId, timeProvider.GetUtcNow()), ct);

        var ignored = stored.LastModifiedAt > order.ModifiedAt;
        var changes = stored.Apply(order.Status, order.ModifiedAt, await SnapshotAsync(channelId, order, ct));

        var itemIds = changes.Select(c => c.ItemId).Distinct().ToList();
        var items = await db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        // Items are always locked in the same order, so two orders sharing items can't deadlock.
        foreach (var change in changes.OrderBy(c => c.ItemId))
        {
            var level = await db.LockStockLevelAsync(change.ItemId, ct)
                ?? throw new InvalidOperationException($"Item {change.ItemId} has no stock level.");
            var item = items[change.ItemId];

            var movement = change.Quantity < 0
                ? StockMovement.Sale(item, -change.Quantity, order.ModifiedAt, StockMovementSource.WooCommerce, null, externalRef: stored.ExternalId)
                : StockMovement.Return(item, change.Quantity, order.ModifiedAt, StockMovementSource.WooCommerce, null, externalRef: stored.ExternalId);
            db.StockMovements.Add(movement);
            level.Apply(movement);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var unlinked = stored.Lines.Count(l => !l.IsLinked && l.OrderedQuantity > 0);
        return new ProcessOrderResult(ignored, changes.Count, unlinked);
    }

    /// <summary>The order's lines with the items their products are linked to in this shop.</summary>
    private async Task<IReadOnlyList<OrderLineSnapshot>> SnapshotAsync(Guid channelId, ShopOrder order, CancellationToken ct)
    {
        var productIds = order.Lines.Select(l => l.ExternalProductId.Trim()).Distinct().ToList();
        var linked = await db.ItemChannelMappings
            .Where(m => m.ChannelId == channelId && productIds.Contains(m.ExternalId))
            .ToDictionaryAsync(m => m.ExternalId, m => m.ItemId, ct);

        return order.Lines
            .Select(l => new OrderLineSnapshot(
                l.ExternalProductId.Trim(),
                linked.TryGetValue(l.ExternalProductId.Trim(), out var itemId) ? itemId : null,
                l.Quantity))
            .ToList();
    }
}
