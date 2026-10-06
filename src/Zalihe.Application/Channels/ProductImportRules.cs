using Zalihe.Application.Common;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Channels;

public enum ProductImportKind
{
    /// <summary>Already linked to an item in an earlier import; nothing to do.</summary>
    Linked,

    /// <summary>An item with the same SKU exists; it will be linked, and stays unchanged.</summary>
    Match,

    /// <summary>No item has this SKU; a new item can be created from the product.</summary>
    New,

    /// <summary>Can't be imported; <see cref="ProductImportRow.Error"/> says why.</summary>
    Skipped,
}

/// <summary>An existing item the import can link to.</summary>
public record ExistingItem(Guid Id, string Name, string Sku);

public record ProductImportRow(ExternalProduct Product, ProductImportKind Kind, ExistingItem? Item = null, AppError? Error = null);

/// <summary>
/// Decides what happens to each shop product on import, without a database: products are matched
/// to items by SKU, the key both sides share. Existing items are never changed by an import.
/// </summary>
public static class ProductImportRules
{
    public const string NoSku = "channel.product_no_sku";
    public const string SkuTooLong = "channel.product_sku_too_long";
    public const string SkuDuplicate = "channel.product_sku_duplicate";
    public const string ItemLinkedElsewhere = "channel.product_item_linked";

    /// <param name="items">The company's items whose SKU appears among the products.</param>
    /// <param name="linkedItems">Item linked to each product ID already (from earlier imports).</param>
    public static IReadOnlyList<ProductImportRow> Classify(
        IReadOnlyList<ExternalProduct> products,
        IReadOnlyCollection<ExistingItem> items,
        IReadOnlyDictionary<string, Guid> linkedItems)
    {
        var itemsBySku = items.ToDictionary(i => i.Sku, StringComparer.Ordinal);
        var itemsById = items.ToDictionary(i => i.Id);
        var linkedItemIds = linkedItems.Values.ToHashSet();

        // A SKU used by two products can't decide which one an item belongs to.
        var skuCounts = products
            .Select(p => Sku(p.Sku))
            .OfType<string>()
            .GroupBy(s => s, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return products.Select(product =>
        {
            if (linkedItems.TryGetValue(product.ExternalId, out var linkedId))
            {
                return new ProductImportRow(product, ProductImportKind.Linked, itemsById.GetValueOrDefault(linkedId));
            }

            var sku = Sku(product.Sku);
            if (sku is null) return Skipped(product, NoSku);
            if (sku.Length > Item.SkuMaxLength) return Skipped(product, SkuTooLong, new() { ["max"] = Item.SkuMaxLength });
            if (skuCounts[sku] > 1) return Skipped(product, SkuDuplicate);

            if (!itemsBySku.TryGetValue(sku, out var item))
            {
                return new ProductImportRow(product, ProductImportKind.New);
            }

            return linkedItemIds.Contains(item.Id)
                ? new ProductImportRow(product, ProductImportKind.Skipped, item, new AppError(ItemLinkedElsewhere, "sku"))
                : new ProductImportRow(product, ProductImportKind.Match, item);
        }).ToList();
    }

    /// <summary>The SKU as items store it: trimmed, null when empty.</summary>
    public static string? Sku(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Text that fits an item field: trimmed and cut to the field's length; null when empty.</summary>
    public static string? Fit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength].TrimEnd();
    }

    /// <summary>A shop price as an item price: two decimals, null when missing or negative.</summary>
    public static decimal? Price(decimal? value) =>
        value is { } price && price >= 0 ? decimal.Round(price, Item.MoneyDecimals) : null;

    /// <summary>Opening stock for a new item: the shop's quantity with at most three decimals, or zero.</summary>
    public static decimal OpeningStock(decimal? value) => value is { } stock ? decimal.Round(stock, Item.QuantityDecimals) : 0;

    private static ProductImportRow Skipped(ExternalProduct product, string code, Dictionary<string, object>? parameters = null) =>
        new(product, ProductImportKind.Skipped, Error: new AppError(code, "sku", parameters));
}
