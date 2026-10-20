using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Stock;

/// <param name="Before">The stock that was shown.</param>
/// <param name="After">The sum of the item's movements, shown from now on.</param>
public record CorrectedStockDto(Guid ItemId, string Name, string Sku, Unit Unit, decimal Before, decimal After);

/// <param name="CheckedCount">Items checked: all of the company's, active or not.</param>
/// <param name="Corrected">Items whose stock didn't match their movements; empty when everything matched.</param>
public record StockRecalculationDto(int CheckedCount, IReadOnlyList<CorrectedStockDto> Corrected);

/// <summary>
/// The administrative recalculation (SPEC 4): the stock of every item must equal the sum of its
/// movements. Finds the items where it doesn't, then fixes each one under the same lock as a new
/// movement, so a movement saved meanwhile is counted. Movements are never changed or added. A
/// correction means a bug somewhere, so it is logged as a warning.
/// </summary>
public class StockRecalculationService(IAppDbContext db, StockPushService stockPush, TimeProvider timeProvider,
    ILogger<StockRecalculationService> logger)
{
    public async Task<StockRecalculationDto> RecalculateAsync(CancellationToken ct)
    {
        var rows = await (
                from level in db.StockLevels.AsNoTracking()
                select new
                {
                    level.ItemId,
                    level.Quantity,
                    Sum = db.StockMovements.Where(m => m.ItemId == level.ItemId).Sum(m => m.Quantity),
                })
            .ToListAsync(ct);
        var suspects = rows.Where(r => r.Quantity != r.Sum).Select(r => r.ItemId).OrderBy(id => id).ToList();
        if (suspects.Count == 0) return new StockRecalculationDto(rows.Count, []);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var corrected = new List<(Guid ItemId, decimal Before, decimal After)>();
        var now = timeProvider.GetUtcNow();
        // Locked in a fixed order (by ID), like orders do, so two runs can't deadlock.
        foreach (var itemId in suspects)
        {
            var level = await db.LockStockLevelAsync(itemId, ct);
            if (level is null) continue;

            var before = level.Quantity;
            var sum = await db.StockMovements.Where(m => m.ItemId == itemId).SumAsync(m => m.Quantity, ct);
            if (level.Reconcile(sum, now) != 0) corrected.Add((itemId, before, sum));
        }

        var shops = await stockPush.MarkChangedAsync(corrected.Select(c => c.ItemId), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        stockPush.Schedule(shops);

        var ids = corrected.Select(c => c.ItemId).ToList();
        var items = await db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var result = corrected
            .Select(c => new CorrectedStockDto(c.ItemId, items[c.ItemId].Name, items[c.ItemId].Sku, items[c.ItemId].Unit, c.Before, c.After))
            .OrderBy(c => c.Name)
            .ToList();
        foreach (var c in result)
        {
            logger.LogWarning("Stock of item {ItemId} was {Before} but its movements sum to {After}; corrected", c.ItemId, c.Before, c.After);
        }

        return new StockRecalculationDto(rows.Count, result);
    }
}
