using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Stock;

/// <summary>
/// Current stock of one item: a cache of the sum of its movements, updated in the same
/// transaction as each new movement. Never set directly.
/// </summary>
public class StockLevel : ITenantOwned
{
    public Guid ItemId { get; private set; }
    public Guid TenantId { get; private set; }
    public decimal Quantity { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token: two simultaneous movements can't overwrite each other.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>A new item starts with zero stock.</summary>
    public StockLevel(Item item, DateTimeOffset createdAt)
    {
        ItemId = item.Id;
        TenantId = item.TenantId;
        Quantity = 0;
        UpdatedAt = createdAt;
    }

    // For EF Core.
    private StockLevel()
    {
    }

    /// <summary>
    /// The administrative recalculation (SPEC 4): sets the cache back to the sum of the item's movements,
    /// should it ever differ. Movements are never touched. Returns the difference that was fixed (0 when
    /// the stock was right).
    /// </summary>
    public decimal Reconcile(decimal sumOfMovements, DateTimeOffset at)
    {
        var difference = sumOfMovements - Quantity;
        if (difference == 0) return 0;

        Quantity = sumOfMovements;
        UpdatedAt = at;
        return difference;
    }

    public void Apply(StockMovement movement)
    {
        if (movement.ItemId != ItemId) throw new InvalidOperationException("The movement belongs to another item.");

        Quantity += movement.Quantity;
        UpdatedAt = movement.OccurredAt > UpdatedAt ? movement.OccurredAt : UpdatedAt;
    }
}
