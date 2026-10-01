using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Dashboard;

public record ReorderItemDto(Guid ItemId, string Name, string Sku, Unit Unit, decimal Stock, decimal MinStock, StockStatus Status);

public record RecentMovementDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    StockMovementType Type,
    decimal Quantity,
    DateTimeOffset OccurredAt,
    StockMovementSource Source,
    string? ExternalRef,
    string? Note);

/// <param name="StockValue">Value of active items at purchase price (only positive stock with a price counts).</param>
public record DashboardDto(
    decimal StockValue,
    int ActiveItems,
    int BelowMinimum,
    int OutOfStock,
    IReadOnlyList<ReorderItemDto> Reorder,
    IReadOnlyList<RecentMovementDto> RecentMovements);

/// <summary>The home screen overview of the current company, in a handful of queries.</summary>
public class DashboardService(IAppDbContext db)
{
    public const int ReorderCount = 5;
    public const int RecentMovementCount = 6;

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var active =
            from item in db.Items.AsNoTracking()
            where item.IsActive
            join level in db.StockLevels on item.Id equals level.ItemId
            select new { item.Id, item.Name, item.Sku, item.Unit, item.MinStock, item.PurchasePrice, Stock = level.Quantity };

        var activeItems = await active.CountAsync(ct);
        var outOfStock = await active.CountAsync(a => a.Stock <= 0, ct);
        var belowMinimum = await active.CountAsync(a => a.Stock > 0 && a.Stock <= a.MinStock, ct);
        var stockValue = await active
            .Where(a => a.PurchasePrice != null && a.Stock > 0)
            .SumAsync(a => a.Stock * a.PurchasePrice!.Value, ct);

        // Most urgent first: out of stock, then the lowest share of the minimum that is left.
        var reorderRows = await active
            .Where(a => a.Stock <= 0 || a.Stock <= a.MinStock)
            .OrderBy(a => a.Stock <= 0 ? 0 : 1)
            .ThenBy(a => a.MinStock == 0 ? 1 : a.Stock / a.MinStock)
            .ThenBy(a => a.Name)
            .Take(ReorderCount)
            .ToListAsync(ct);
        var reorder = reorderRows
            .Select(a => new ReorderItemDto(a.Id, a.Name, a.Sku, a.Unit, a.Stock, a.MinStock, StockStatusRules.For(a.Stock, a.MinStock)))
            .ToList();

        var recent = await (
                from movement in db.StockMovements.AsNoTracking()
                join item in db.Items on movement.ItemId equals item.Id
                orderby movement.OccurredAt descending, movement.Id descending
                select new RecentMovementDto(movement.Id, item.Id, item.Name, movement.Type, movement.Quantity,
                    movement.OccurredAt, movement.Source, movement.ExternalRef, movement.Note))
            .Take(RecentMovementCount)
            .ToListAsync(ct);

        return new DashboardDto(decimal.Round(stockValue, Item.MoneyDecimals), activeItems, belowMinimum, outOfStock, reorder, recent);
    }
}
