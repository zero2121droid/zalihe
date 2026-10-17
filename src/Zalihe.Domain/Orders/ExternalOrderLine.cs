using Zalihe.Domain.Channels;
using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Orders;

/// <summary>
/// One product of an order. <see cref="AppliedQuantity"/> is how much stock the line has
/// already taken, so a new event only applies the difference.
/// </summary>
public class ExternalOrderLine : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ExternalOrderId { get; private set; }

    public const int NameMaxLength = Item.NameMaxLength;

    /// <summary>The product's (or variation's) ID in the shop.</summary>
    public string ExternalProductId { get; private set; }

    /// <summary>
    /// The product's name in the shop when the order last arrived, e.g. "Duks sa kapuljačom - Plavi, L".
    /// Shown for lines whose product isn't linked; null for orders that arrived before names were kept.
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>The linked item; null while the product isn't linked, and nothing is taken from stock.</summary>
    public Guid? ItemId { get; private set; }

    /// <summary>How much the order holds now; 0 when the line was removed from the order.</summary>
    public decimal OrderedQuantity { get; private set; }

    /// <summary>How much has already been taken from the item's stock.</summary>
    public decimal AppliedQuantity { get; private set; }

    public bool IsLinked => ItemId is not null;

    internal ExternalOrderLine(ExternalOrder order, string externalProductId, Guid? itemId, decimal quantity, string? name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProductId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(externalProductId.Length, ItemChannelMapping.ExternalIdMaxLength, nameof(externalProductId));

        Id = Guid.CreateVersion7();
        TenantId = order.TenantId;
        ExternalOrderId = order.Id;
        ExternalProductId = externalProductId;
        Update(itemId, quantity, name);
    }

    // For EF Core.
    private ExternalOrderLine()
    {
        ExternalProductId = null!;
    }

    internal void Update(Guid? itemId, decimal quantity, string? name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        if (!Item.HasAtMostDecimals(quantity, Item.QuantityDecimals))
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Too many decimal places.");
        }

        OrderedQuantity = quantity;
        if (!string.IsNullOrWhiteSpace(name))
        {
            name = name.Trim();
            Name = name.Length > NameMaxLength ? name[..NameMaxLength] : name;
        }

        // Stock already taken stays with the item it was taken from, so it is returned to the same item.
        if (AppliedQuantity == 0)
        {
            ItemId = itemId;
        }
    }

    /// <summary>Takes or returns the difference between what the line should hold and what it holds.</summary>
    internal OrderStockChange? Settle(bool orderIsActive)
    {
        if (ItemId is not { } itemId) return null;

        var desired = orderIsActive ? OrderedQuantity : 0;
        var delta = desired - AppliedQuantity;
        if (delta == 0) return null;

        AppliedQuantity = desired;
        return new OrderStockChange(itemId, -delta);
    }
}
