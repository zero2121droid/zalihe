using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Reports;

/// <param name="StockValue">Value of active items at purchase price (only positive stock with a price counts).</param>
/// <param name="ItemsWithoutPrice">Active items in stock without a purchase price, so missing from <paramref name="StockValue"/>.</param>
/// <param name="SlowMoverCount">Items in <see cref="ReportService.ListSlowMoversAsync"/>.</param>
/// <param name="SlowMoverValue">The value tied up in them, at purchase price.</param>
public record ReportSummaryDto(decimal StockValue, int ItemsWithoutPrice, int SlowMoverCount, decimal SlowMoverValue);

/// <param name="Sold">Sold in the last <see cref="ReportService.TopSellerDays"/> days, less what came back.</param>
public record TopSellerDto(Guid ItemId, string Name, string Sku, Unit Unit, decimal Sold, decimal Stock, StockStatus Status);

/// <param name="Value">Stock at purchase price; null without a purchase price.</param>
/// <param name="LastSoldAt">The item's last sale ever; null when it was never sold.</param>
public record SlowMoverDto(Guid ItemId, string Name, string Sku, Unit Unit, decimal Stock, decimal? Value, DateTimeOffset? LastSoldAt);

/// <param name="Category">Null for items without a category.</param>
/// <param name="ItemCount">Active items in the category.</param>
/// <param name="ItemsWithoutPrice">Of them, in stock without a purchase price, so missing from <paramref name="Value"/>.</param>
public record CategoryValueDto(string? Category, int ItemCount, decimal Value, int ItemsWithoutPrice);

/// <param name="Search">Part of the item's name or SKU.</param>
/// <param name="Category">Only items in this category.</param>
public record ReportQuery(
    string? Search = null,
    string? Category = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);

/// <summary>
/// Reports over active items (SPEC 2.6): what sells best, what doesn't sell at all, and what the
/// stock is worth. Sold quantities are sales less returns, so a cancelled order counts as not sold.
/// </summary>
public class ReportService(IAppDbContext db, TimeProvider timeProvider)
{
    public const int TopSellerDays = 30;

    /// <summary>
    /// An item in stock with no sale for this many days. Items added in that time are left out:
    /// they haven't had the chance to sell yet.
    /// </summary>
    public const int SlowMoverDays = 60;

    public async Task<ReportSummaryDto> GetSummaryAsync(string? category, CancellationToken ct)
    {
        var inStock = WithStock(Filter(new ReportQuery(Category: category))).Where(r => r.Stock > 0);

        var stockValue = await inStock
            .Where(r => r.Item.PurchasePrice != null)
            .SumAsync(r => r.Stock * r.Item.PurchasePrice!.Value, ct);
        var withoutPrice = await inStock.CountAsync(r => r.Item.PurchasePrice == null, ct);

        var slow = SlowMovers(Filter(new ReportQuery(Category: category)));
        var slowCount = await slow.CountAsync(ct);
        var slowValue = await slow
            .Where(r => r.Item.PurchasePrice != null)
            .SumAsync(r => r.Stock * r.Item.PurchasePrice!.Value, ct);

        return new ReportSummaryDto(Money(stockValue), withoutPrice, slowCount, Money(slowValue));
    }

    /// <summary>Items that sold the most in the last <see cref="TopSellerDays"/> days, most first.</summary>
    public async Task<PagedResult<TopSellerDto>> ListTopSellersAsync(ReportQuery query, CancellationToken ct)
    {
        var since = timeProvider.GetUtcNow().AddDays(-TopSellerDays);
        // Same as ItemService's sold in the last 30 days: sales less returns.
        var sold =
            from movement in db.StockMovements
            where (movement.Type == StockMovementType.Sale || movement.Type == StockMovementType.Return)
                  && movement.OccurredAt >= since
            group movement by movement.ItemId into byItem
            select new { ItemId = byItem.Key, Sold = -byItem.Sum(m => m.Quantity) };

        var rows =
            from s in sold
            where s.Sold > 0
            join row in WithStock(Filter(query)) on s.ItemId equals row.Item.Id
            select new { row.Item, row.Stock, s.Sold };

        var totalCount = await rows.CountAsync(ct);
        var page = await rows
            .OrderByDescending(r => r.Sold)
            .ThenBy(r => r.Item.Name)
            .ThenBy(r => r.Item.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = page
            .Select(r => new TopSellerDto(r.Item.Id, r.Item.Name, r.Item.Sku, r.Item.Unit, r.Sold, r.Stock,
                StockStatusRules.For(r.Stock, r.Item.MinStock)))
            .ToList();
        return new PagedResult<TopSellerDto>(items, totalCount, query.Page, query.PageSize);
    }

    /// <summary>
    /// Items in stock with no sale in the last <see cref="SlowMoverDays"/> days, the most money
    /// tied up first (items without a purchase price last).
    /// </summary>
    public async Task<PagedResult<SlowMoverDto>> ListSlowMoversAsync(ReportQuery query, CancellationToken ct)
    {
        var rows = SlowMovers(Filter(query));

        var totalCount = await rows.CountAsync(ct);
        var page = await rows
            .OrderBy(r => r.Item.PurchasePrice == null)
            .ThenByDescending(r => r.Stock * r.Item.PurchasePrice)
            .ThenBy(r => r.Item.Name)
            .ThenBy(r => r.Item.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new
            {
                r.Item,
                r.Stock,
                LastSoldAt = db.StockMovements
                    .Where(m => m.ItemId == r.Item.Id && m.Type == StockMovementType.Sale)
                    .Max(m => (DateTimeOffset?)m.OccurredAt),
            })
            .ToListAsync(ct);

        var items = page
            .Select(r => new SlowMoverDto(r.Item.Id, r.Item.Name, r.Item.Sku, r.Item.Unit, r.Stock,
                r.Item.PurchasePrice is { } price ? Money(r.Stock * price) : null, r.LastSoldAt))
            .ToList();
        return new PagedResult<SlowMoverDto>(items, totalCount, query.Page, query.PageSize);
    }

    /// <summary>Stock value per category, the most valuable first; items without a category are one group.</summary>
    public async Task<PagedResult<CategoryValueDto>> ListCategoryValuesAsync(int page, int pageSize, CancellationToken ct)
    {
        var categories =
            from row in WithStock(Filter(new ReportQuery()))
            group row by row.Item.Category into byCategory
            select new
            {
                Category = byCategory.Key,
                ItemCount = byCategory.Count(),
                Value = byCategory.Sum(r => r.Stock > 0 && r.Item.PurchasePrice != null ? r.Stock * r.Item.PurchasePrice.Value : 0),
                ItemsWithoutPrice = byCategory.Count(r => r.Stock > 0 && r.Item.PurchasePrice == null),
            };

        var totalCount = await categories.CountAsync(ct);
        var rows = await categories
            .OrderByDescending(c => c.Value)
            .ThenBy(c => c.Category == null)
            .ThenBy(c => c.Category)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(c => new CategoryValueDto(c.Category, c.ItemCount, Money(c.Value), c.ItemsWithoutPrice)).ToList();
        return new PagedResult<CategoryValueDto>(items, totalCount, page, pageSize);
    }

    private IQueryable<Item> Filter(ReportQuery query)
    {
        var items = db.Items.AsNoTracking().Where(i => i.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            items = items.Where(i => i.Name.ToLower().Contains(term) || i.Sku.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            items = items.Where(i => i.Category == category);
        }

        return items;
    }

    private sealed class ItemWithStock
    {
        public required Item Item { get; init; }
        public decimal Stock { get; init; }
    }

    private IQueryable<ItemWithStock> WithStock(IQueryable<Item> items) =>
        from item in items
        join level in db.StockLevels on item.Id equals level.ItemId
        select new ItemWithStock { Item = item, Stock = level.Quantity };

    private IQueryable<ItemWithStock> SlowMovers(IQueryable<Item> items)
    {
        var since = timeProvider.GetUtcNow().AddDays(-SlowMoverDays);
        return WithStock(items.Where(i => i.CreatedAt < since))
            .Where(r => r.Stock > 0)
            .Where(r => !db.StockMovements.Any(m => m.ItemId == r.Item.Id && m.Type == StockMovementType.Sale && m.OccurredAt >= since));
    }

    private static decimal Money(decimal value) => decimal.Round(value, Item.MoneyDecimals);
}
