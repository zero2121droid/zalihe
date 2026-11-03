using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Orders;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Orders;

/// <summary>Records an order's stock changes, the same way for shop and manual orders.</summary>
internal static class OrderMovements
{
    /// <summary>
    /// Records a sale or return for every change and updates the stock, inside the caller's transaction;
    /// returns the items that changed. Items are always locked in the same order, so two orders sharing
    /// items can't deadlock.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> RecordAsync(
        IAppDbContext db, IReadOnlyList<OrderStockChange> changes, DateTimeOffset occurredAt,
        StockMovementSource source, Guid? userId, string externalRef, CancellationToken ct)
    {
        var itemIds = changes.Select(c => c.ItemId).Distinct().ToList();
        var items = await db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        foreach (var change in changes.OrderBy(c => c.ItemId))
        {
            var level = await db.LockStockLevelAsync(change.ItemId, ct)
                ?? throw new InvalidOperationException($"Item {change.ItemId} has no stock level.");
            var item = items[change.ItemId];

            var movement = change.Quantity < 0
                ? StockMovement.Sale(item, -change.Quantity, occurredAt, source, userId, externalRef: externalRef)
                : StockMovement.Return(item, change.Quantity, occurredAt, source, userId, externalRef: externalRef);
            db.StockMovements.Add(movement);
            level.Apply(movement);
        }

        return itemIds;
    }
}
