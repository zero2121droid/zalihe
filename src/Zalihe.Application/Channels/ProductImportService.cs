using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Channels;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Channels;

/// <summary>A shop product that will be linked to the existing item with the same SKU.</summary>
public record ProductToLinkDto(string ExternalId, string Name, string Sku, Guid ItemId, string ItemName);

/// <summary>A shop product that can become a new item; <see cref="Stock"/> becomes its opening stock.</summary>
public record ProductToCreateDto(string ExternalId, string Name, string? GroupName, string Sku, string? Category, decimal? Price, decimal Stock);

public record SkippedProductDto(string ExternalId, string Name, string? Sku, AppError Error);

public record ProductImportPreviewDto(
    int LinkedCount,
    IReadOnlyList<ProductToLinkDto> ToLink,
    IReadOnlyList<ProductToCreateDto> ToCreate,
    IReadOnlyList<SkippedProductDto> Skipped);

public record ProductImportResultDto(int LinkedCount, int CreatedCount, int SkippedCount);

public record ProductImportResult<T>(T? Value, AppError? Error, bool NotFound = false)
{
    public static ProductImportResult<T> Missing { get; } = new(default, null, NotFound: true);
}

/// <summary>
/// Imports a shop's products: links them to items with the same SKU and creates the new ones the
/// user picked, with the shop's stock as opening stock. Like the CSV import it reads the source
/// again on import, so nothing is kept between preview and import. Existing items never change.
/// </summary>
public class ProductImportService(
    IAppDbContext db,
    ITenantContext tenant,
    ICredentialProtector protector,
    ISalesChannelFactory channels,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public const int MaxProducts = 5000;
    public const string OpeningStockNote = "Početno stanje";

    public async Task<ProductImportResult<ProductImportPreviewDto>> PreviewAsync(Guid channelId, CancellationToken ct)
    {
        var read = await ReadAsync(channelId, ct);
        if (read.NotFound) return ProductImportResult<ProductImportPreviewDto>.Missing;
        if (read.Error is not null) return new(null, read.Error);

        var rows = read.Value!.Rows;
        var preview = new ProductImportPreviewDto(
            rows.Count(r => r.Kind == ProductImportKind.Linked),
            rows.Where(r => r.Kind == ProductImportKind.Match)
                .Select(r => new ProductToLinkDto(r.Product.ExternalId, r.Product.Name, r.Item!.Sku, r.Item.Id, r.Item.Name))
                .ToList(),
            rows.Where(r => r.Kind == ProductImportKind.New).Select(ToCreate).ToList(),
            rows.Where(r => r.Kind == ProductImportKind.Skipped)
                .Select(r => new SkippedProductDto(r.Product.ExternalId, r.Product.Name, ProductImportRules.Sku(r.Product.Sku), r.Error!))
                .ToList());
        return new(preview, null);
    }

    /// <param name="createExternalIds">The new products the user chose to create; others are left out.</param>
    public async Task<ProductImportResult<ProductImportResultDto>> ImportAsync(
        Guid channelId, IReadOnlyCollection<string> createExternalIds, CancellationToken ct)
    {
        var read = await ReadAsync(channelId, ct);
        if (read.NotFound) return ProductImportResult<ProductImportResultDto>.Missing;
        if (read.Error is not null) return new(null, read.Error);

        var (channel, rows) = read.Value!;
        var selected = createExternalIds.ToHashSet(StringComparer.Ordinal);
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        var matches = rows.Where(r => r.Kind == ProductImportKind.Match).ToList();
        var matchedIds = matches.Select(r => r.Item!.Id).ToList();
        var matchedItems = await db.Items.Where(i => matchedIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        foreach (var row in matches)
        {
            db.ItemChannelMappings.Add(Mapping(matchedItems[row.Item!.Id], channel, row.Product, now));
        }

        var toCreate = rows.Where(r => r.Kind == ProductImportKind.New && selected.Contains(r.Product.ExternalId)).ToList();
        foreach (var row in toCreate)
        {
            var data = ToCreate(row);
            var item = new Item(tenant.TenantId, data.Name, data.Sku, Unit.Kom, 0, now,
                category: data.Category, groupName: data.GroupName, salePrice: data.Price);
            var level = new StockLevel(item, now);
            db.Items.Add(item);
            db.StockLevels.Add(level);
            db.ItemChanges.Add(ItemChange.Created(item, userId, now));
            db.ItemChannelMappings.Add(Mapping(item, channel, row.Product, now));

            if (data.Stock != 0)
            {
                var opening = StockMovement.Adjustment(item, data.Stock, now, StockMovementSource.WooCommerce, userId, OpeningStockNote);
                db.StockMovements.Add(opening);
                level.Apply(opening);
            }
        }

        // One SaveChanges: links and new items are saved together or not at all.
        await db.SaveChangesAsync(ct);
        var skipped = rows.Count(r => r.Kind == ProductImportKind.Skipped);
        return new(new ProductImportResultDto(matches.Count, toCreate.Count, skipped), null);
    }

    private sealed record ShopProducts(SalesChannel Channel, IReadOnlyList<ProductImportRow> Rows);

    private async Task<ProductImportResult<ShopProducts>> ReadAsync(Guid channelId, CancellationToken ct)
    {
        // The tenant filter makes another company's shop "not found".
        var channel = await db.SalesChannels.SingleOrDefaultAsync(c => c.Id == channelId, ct);
        if (channel is null) return ProductImportResult<ShopProducts>.Missing;

        IReadOnlyList<ExternalProduct> products;
        try
        {
            products = await channels.Create(channel, protector).FetchProductsAsync(MaxProducts, ct);
        }
        catch (SalesChannelException e)
        {
            var parameters = e.Code == SalesChannelException.TooManyProducts
                ? new Dictionary<string, object> { ["max"] = MaxProducts }
                : null;
            return new(null, new AppError(e.Code, null, parameters));
        }

        var linked = await db.ItemChannelMappings
            .Where(m => m.ChannelId == channel.Id)
            .ToDictionaryAsync(m => m.ExternalId, m => m.ItemId, StringComparer.Ordinal, ct);

        // Items with a SKU from the shop, and the already linked ones (to show their names).
        var skus = products.Select(p => ProductImportRules.Sku(p.Sku)).OfType<string>().Distinct().ToList();
        var linkedIds = linked.Values.ToList();
        var items = await db.Items
            .Where(i => skus.Contains(i.Sku) || linkedIds.Contains(i.Id))
            .Select(i => new ExistingItem(i.Id, i.Name, i.Sku))
            .ToListAsync(ct);

        return new(new ShopProducts(channel, ProductImportRules.Classify(products, items, linked)), null);
    }

    private static ProductToCreateDto ToCreate(ProductImportRow row) => new(
        row.Product.ExternalId,
        ProductImportRules.Fit(row.Product.Name, Item.NameMaxLength) ?? ProductImportRules.Sku(row.Product.Sku)!,
        ProductImportRules.Fit(row.Product.GroupName, Item.GroupNameMaxLength),
        ProductImportRules.Sku(row.Product.Sku)!,
        ProductImportRules.Fit(row.Product.Category, Item.CategoryMaxLength),
        ProductImportRules.Price(row.Product.Price),
        ProductImportRules.OpeningStock(row.Product.Stock));

    private static ItemChannelMapping Mapping(Item item, SalesChannel channel, ExternalProduct product, DateTimeOffset now) =>
        new(item, channel, product.ExternalId, product.ParentExternalId, now);
}
