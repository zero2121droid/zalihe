namespace Zalihe.Domain.Stock;

public enum StockMovementType
{
    /// <summary>Goods received; quantity is positive.</summary>
    Receipt,

    /// <summary>Goods sold or issued; quantity is negative.</summary>
    Sale,

    /// <summary>Correction after a count, write-off or damage; quantity has either sign.</summary>
    Adjustment,

    /// <summary>Goods returned by a customer; quantity is positive.</summary>
    Return,
}

/// <summary>Where a movement came from.</summary>
public enum StockMovementSource
{
    Manual,
    Csv,
    WooCommerce,
}
