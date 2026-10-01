using System.Globalization;
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

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        IsActive = true;
        CreatedAt = createdAt;
        Name = null!;
        Sku = null!;
        Update(name, sku, unit, minStock, barcode, category, groupName, purchasePrice, salePrice);
    }

    /// <summary>
    /// Changes the item's data. The same rules apply as when creating it.
    /// Returns the fields that actually changed, for the item's change log.
    /// </summary>
    public IReadOnlyList<ItemFieldChange> Update(
        string name,
        string sku,
        Unit unit,
        decimal minStock,
        string? barcode = null,
        string? category = null,
        string? groupName = null,
        decimal? purchasePrice = null,
        decimal? salePrice = null)
    {
        if (!Enum.IsDefined(unit)) throw new ArgumentOutOfRangeException(nameof(unit));

        // Validate everything first, so a rejected update leaves the item unchanged.
        var newName = Required(name, NameMaxLength, nameof(name));
        var newSku = Required(sku, SkuMaxLength, nameof(sku));
        var newBarcode = Optional(barcode, BarcodeMaxLength, nameof(barcode));
        var newCategory = Optional(category, CategoryMaxLength, nameof(category));
        var newGroupName = Optional(groupName, GroupNameMaxLength, nameof(groupName));
        var newPurchasePrice = Money(purchasePrice, nameof(purchasePrice));
        var newSalePrice = Money(salePrice, nameof(salePrice));
        var newMinStock = Quantity(minStock, nameof(minStock));

        var changes = new List<ItemFieldChange>();
        Track(changes, "name", Name, newName);
        Track(changes, "sku", Sku, newSku);
        Track(changes, "barcode", Barcode, newBarcode);
        Track(changes, "unit", Code(Unit), Code(unit));
        Track(changes, "category", Category, newCategory);
        Track(changes, "groupName", GroupName, newGroupName);
        Track(changes, "purchasePrice", Invariant(PurchasePrice), Invariant(newPurchasePrice));
        Track(changes, "salePrice", Invariant(SalePrice), Invariant(newSalePrice));
        Track(changes, "minStock", Invariant(MinStock), Invariant(newMinStock));

        Name = newName;
        Sku = newSku;
        Barcode = newBarcode;
        Unit = unit;
        Category = newCategory;
        GroupName = newGroupName;
        PurchasePrice = newPurchasePrice;
        SalePrice = newSalePrice;
        MinStock = newMinStock;
        return changes;
    }

    /// <summary>
    /// Hides the item from lists and pickers. Items are never deleted, because their stock
    /// history must stay; a deactivated item can be activated again.
    /// </summary>
    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    // For EF Core.
    private Item()
    {
        Name = null!;
        Sku = null!;
    }

    // Values are recorded in a language-neutral form; clients format them for the user's language.
    private void Track(List<ItemFieldChange> changes, string field, string? oldValue, string? newValue)
    {
        // A new item has no previous values (Name is null before the first Update): nothing to log.
        if (Name is null || oldValue == newValue) return;
        changes.Add(new ItemFieldChange { Field = field, OldValue = oldValue, NewValue = newValue });
    }

    private static string Code(Unit unit) => unit.ToString().ToLowerInvariant();

    // "G29" drops trailing zeros: 12.50 and 12.5 are the same value.
    private static string? Invariant(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);

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
