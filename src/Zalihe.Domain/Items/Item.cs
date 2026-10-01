using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Items;

/// <summary>
/// A stock item. Each WooCommerce variation (e.g. T-shirt M blue) is a separate item;
/// <see cref="GroupName"/> only groups them in the UI.
/// </summary>
public class Item : ITenantOwned
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 64;
    public const int BarcodeMaxLength = 64;
    public const int CategoryMaxLength = 100;
    public const int GroupNameMaxLength = 200;
    public const int QuantityDecimals = 3;
    public const int MoneyDecimals = 2;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string Sku { get; private set; }
    public string? Barcode { get; private set; }
    public Unit Unit { get; private set; }
    public string? Category { get; private set; }
    public string? GroupName { get; private set; }
    public decimal? PurchasePrice { get; private set; }
    public decimal? SalePrice { get; private set; }
    public decimal MinStock { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Item(
        Guid tenantId,
        string name,
        string sku,
        Unit unit,
        decimal minStock,
        DateTimeOffset createdAt,
        string? barcode = null,
        string? category = null,
        string? groupName = null,
        decimal? purchasePrice = null,
        decimal? salePrice = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (!Enum.IsDefined(unit)) throw new ArgumentOutOfRangeException(nameof(unit));

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Name = Required(name, NameMaxLength, nameof(name));
        Sku = Required(sku, SkuMaxLength, nameof(sku));
        Barcode = Optional(barcode, BarcodeMaxLength, nameof(barcode));
        Unit = unit;
        Category = Optional(category, CategoryMaxLength, nameof(category));
        GroupName = Optional(groupName, GroupNameMaxLength, nameof(groupName));
        PurchasePrice = Money(purchasePrice, nameof(purchasePrice));
        SalePrice = Money(salePrice, nameof(salePrice));
        MinStock = Quantity(minStock, nameof(minStock));
        IsActive = true;
        CreatedAt = createdAt;
    }

    // For EF Core.
    private Item()
    {
        Name = null!;
        Sku = null!;
    }

    /// <summary>True when the value has no more decimals than <paramref name="decimals"/>.</summary>
    public static bool HasAtMostDecimals(decimal value, int decimals) => decimal.Round(value, decimals) == value;

    private static string Required(string value, int maxLength, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        value = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, maxLength, paramName);
        return value;
    }

    private static string? Optional(string? value, int maxLength, string paramName) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maxLength, paramName);

    private static decimal Quantity(decimal value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        if (!HasAtMostDecimals(value, QuantityDecimals)) throw new ArgumentOutOfRangeException(paramName);
        return value;
    }

    private static decimal? Money(decimal? value, string paramName)
    {
        if (value is null) return null;
        ArgumentOutOfRangeException.ThrowIfNegative(value.Value, paramName);
        if (!HasAtMostDecimals(value.Value, MoneyDecimals)) throw new ArgumentOutOfRangeException(paramName);
        return value;
    }
}
