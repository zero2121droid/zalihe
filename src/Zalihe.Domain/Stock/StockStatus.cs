namespace Zalihe.Domain.Stock;

/// <summary>Stock status shown next to every item (always as color and text).</summary>
public enum StockStatus
{
    InStock,

    /// <summary>Above zero, but at or below the minimum: time to reorder.</summary>
    Low,

    /// <summary>Zero or below (stock may go negative when web orders arrive).</summary>
    OutOfStock,
}

public static class StockStatusRules
{
    // The same conditions are used as SQL filters in ItemService; keep them in sync
    // (ItemsTests checks that the filter and the status agree).
    public static StockStatus For(decimal quantity, decimal minStock) =>
        quantity <= 0 ? StockStatus.OutOfStock
        : quantity <= minStock ? StockStatus.Low
        : StockStatus.InStock;
}
