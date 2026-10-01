using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Stock;

/// <summary>
/// One change of stock. Movements are only ever added: never changed, never deleted.
/// A mistake is fixed with a new movement (an adjustment). The current stock of an item
/// is always the sum of its movements.
/// </summary>
public class StockMovement : ITenantOwned
{
    public const int NoteMaxLength = 500;
    public const int ExternalRefMaxLength = 100;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ItemId { get; private set; }
    public StockMovementType Type { get; private set; }

    /// <summary>Signed: positive adds to stock, negative takes from it.</summary>
    public decimal Quantity { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
    public StockMovementSource Source { get; private set; }
    public string? ExternalRef { get; private set; }
    public string? Note { get; private set; }

    /// <summary>The user who entered it; null for movements from integrations.</summary>
    public Guid? UserId { get; private set; }

    private StockMovement(
        Item item,
        StockMovementType type,
        decimal quantity,
        DateTimeOffset occurredAt,
        StockMovementSource source,
        Guid? userId,
        string? note,
        string? externalRef)
    {
        if (!Item.HasAtMostDecimals(quantity, Item.QuantityDecimals))
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Too many decimal places.");
        }

        Id = Guid.CreateVersion7();
        TenantId = item.TenantId;
        ItemId = item.Id;
        Type = type;
        Quantity = quantity;
        OccurredAt = occurredAt;
        Source = source;
        UserId = userId;
        Note = Optional(note, NoteMaxLength, nameof(note));
        ExternalRef = Optional(externalRef, ExternalRefMaxLength, nameof(externalRef));
    }

    // For EF Core.
    private StockMovement()
    {
    }

    /// <param name="quantity">How much was received; must be positive.</param>
    public static StockMovement Receipt(Item item, decimal quantity, DateTimeOffset occurredAt,
        StockMovementSource source, Guid? userId, string? note = null, string? externalRef = null) =>
        new(item, StockMovementType.Receipt, Positive(quantity), occurredAt, source, userId, note, externalRef);

    /// <param name="quantity">How much was sold, as a positive number; it is stored as negative.</param>
    public static StockMovement Sale(Item item, decimal quantity, DateTimeOffset occurredAt,
        StockMovementSource source, Guid? userId, string? note = null, string? externalRef = null) =>
        new(item, StockMovementType.Sale, -Positive(quantity), occurredAt, source, userId, note, externalRef);

    /// <param name="quantity">How much came back; must be positive.</param>
    public static StockMovement Return(Item item, decimal quantity, DateTimeOffset occurredAt,
        StockMovementSource source, Guid? userId, string? note = null, string? externalRef = null) =>
        new(item, StockMovementType.Return, Positive(quantity), occurredAt, source, userId, note, externalRef);

    /// <summary>A correction by a signed difference (e.g. −2 for damaged goods).</summary>
    public static StockMovement Adjustment(Item item, decimal difference, DateTimeOffset occurredAt,
        StockMovementSource source, Guid? userId, string? note = null)
    {
        if (difference == 0) throw new ArgumentOutOfRangeException(nameof(difference), "An adjustment must change the stock.");
        return new(item, StockMovementType.Adjustment, difference, occurredAt, source, userId, note, null);
    }

    /// <summary>
    /// A correction after counting: the difference between what was counted and the current stock.
    /// Returns null when the count matches the stock, since nothing changes.
    /// </summary>
    public static StockMovement? AdjustmentToCount(Item item, decimal counted, decimal currentStock,
        DateTimeOffset occurredAt, Guid? userId, string? note)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(counted);
        var difference = counted - currentStock;
        return difference == 0 ? null : Adjustment(item, difference, occurredAt, StockMovementSource.Manual, userId, note);
    }

    private static decimal Positive(decimal quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        return quantity;
    }

    private static string? Optional(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, maxLength, paramName);
        return value;
    }
}
