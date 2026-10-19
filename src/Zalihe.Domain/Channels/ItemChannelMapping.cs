using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Channels;

/// <summary>
/// Links an item to a product in a shop: a simple product, or one variation of a variable product.
/// An item is linked to at most one product per shop, and a product to at most one item.
/// </summary>
public class ItemChannelMapping : ITenantOwned
{
    public const int ExternalIdMaxLength = 64;
    public const int ImageUrlMaxLength = 2048;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid ChannelId { get; private set; }

    /// <summary>The product's (or variation's) ID in the shop.</summary>
    public string ExternalId { get; private set; }

    /// <summary>For a variation, the ID of its variable product; null for a simple product.</summary>
    public string? ParentExternalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The product's image in the shop, shown by the shop's own address (not copied); null without one.
    /// Refreshed on every product import. An image the user adds to the item takes precedence.
    /// </summary>
    public string? ImageUrl { get; private set; }

    /// <summary>
    /// When the item's stock last changed without the shop having it yet; null when the shop is up to date.
    /// The stock push clears it only if it is still the value it read, so a change during a push is sent next time.
    /// </summary>
    public DateTimeOffset? StockChangedAt { get; private set; }

    /// <summary>The stock last sent to the shop (whole units); null until the first push.</summary>
    public int? PushedStock { get; private set; }

    public ItemChannelMapping(Item item, SalesChannel channel, string externalId, string? parentExternalId, DateTimeOffset createdAt)
    {
        if (item.TenantId != channel.TenantId) throw new InvalidOperationException("The item and the shop belong to different companies.");

        Id = Guid.CreateVersion7();
        TenantId = item.TenantId;
        ItemId = item.Id;
        ChannelId = channel.Id;
        ExternalId = Required(externalId, nameof(externalId));
        ParentExternalId = string.IsNullOrWhiteSpace(parentExternalId) ? null : Required(parentExternalId, nameof(parentExternalId));
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Sets the shop's image of the product. Shop data is not trusted: anything other than an absolute
    /// http(s) address of a sensible length counts as no image, so a broken product never fails an import.
    /// </summary>
    public void SetImageUrl(string? url) =>
        ImageUrl = Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                   && uri.AbsoluteUri.Length <= ImageUrlMaxLength
            ? uri.AbsoluteUri
            : null;

    /// <summary>The item's stock changed and must be sent to the shop.</summary>
    public void MarkStockChanged(DateTimeOffset at) => StockChangedAt = at;

    /// <summary>
    /// The stock a shop shows for <paramref name="stock"/>: WooCommerce stock is a whole number, so
    /// 2,5 kg shows as 2 (never more than there is).
    /// </summary>
    public static int ShopStock(decimal stock) => (int)Math.Floor(stock);

    // For EF Core.
    private ItemChannelMapping()
    {
        ExternalId = null!;
    }

    private static string Required(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        value = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, ExternalIdMaxLength, paramName);
        return value;
    }
}
