using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Receipts;

/// <summary>One item received on a <see cref="Receipt"/>.</summary>
public class ReceiptLine : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReceiptId { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>The line's place on the receipt, from 0, so lines are shown in the order they were entered.</summary>
    public int Position { get; private set; }

    /// <summary>How much was received; positive.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>The purchase price per unit on the document; null when unknown.</summary>
    public decimal? UnitPrice { get; private set; }

    internal ReceiptLine(Receipt receipt, int position, Item item, decimal quantity, decimal? unitPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (!Item.HasAtMostDecimals(quantity, Item.QuantityDecimals))
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Too many decimal places.");
        }

        if (unitPrice is { } price)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(price, nameof(unitPrice));
            if (!Item.HasAtMostDecimals(price, Item.MoneyDecimals))
            {
                throw new ArgumentOutOfRangeException(nameof(unitPrice), "Too many decimal places.");
            }
        }

        Id = Guid.CreateVersion7();
        TenantId = receipt.TenantId;
        ReceiptId = receipt.Id;
        ItemId = item.Id;
        Position = position;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    // For EF Core.
    private ReceiptLine()
    {
    }
}
