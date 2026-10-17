using Zalihe.Domain.Channels;

namespace Zalihe.Application.Channels;

/// <summary>
/// An online shop as the core sees it (SPEC 5). The core never talks to WooCommerce directly;
/// fetching products, orders and pushing stock are added here in their own steps.
/// </summary>
public interface ISalesChannel
{
    /// <summary>Checks that the shop is reachable and accepts the credentials; throws <see cref="SalesChannelException"/> otherwise.</summary>
    Task CheckConnectionAsync(CancellationToken ct);

    /// <summary>
    /// All products that hold stock: simple products and the variations of variable products
    /// (the variable product itself is only their group). Throws <see cref="SalesChannelException"/>
    /// when the shop can't be read, or <see cref="SalesChannelException.TooManyProducts"/> above <paramref name="max"/>.
    /// </summary>
    Task<IReadOnlyList<ExternalProduct>> FetchProductsAsync(int max, CancellationToken ct);

    /// <summary>
    /// Makes sure the shop sends new and changed orders to <paramref name="deliveryUrl"/>, signed with
    /// <paramref name="secret"/>: creates the webhooks that are missing and re-enables and updates
    /// existing ones (a shop disables a webhook after repeated failed deliveries). Safe to call again.
    /// </summary>
    Task EnsureWebhooksAsync(string deliveryUrl, string secret, CancellationToken ct);

    /// <summary>
    /// Orders the shop changed after <paramref name="since"/> (by the shop's clock), oldest change first,
    /// in every status except deleted. Orders that can't be read are left out, as they are in webhooks.
    /// </summary>
    Task<IReadOnlyList<ShopOrder>> FetchOrdersModifiedSinceAsync(DateTimeOffset since, CancellationToken ct);

    /// <summary>
    /// Sets the stock of products and variations, turning on stock tracking for them. Returns the IDs
    /// the shop accepted; one it rejects (e.g. deleted from the shop) is left out. Throws
    /// <see cref="SalesChannelException"/> when the shop can't be reached at all.
    /// </summary>
    Task<IReadOnlySet<string>> UpdateStockAsync(IReadOnlyList<ShopStockUpdate> updates, CancellationToken ct);
}

/// <param name="ExternalId">The product's ID, or the variation's ID for a variation.</param>
/// <param name="ParentExternalId">The variable product's ID for a variation; null for a simple product.</param>
public record ShopStockUpdate(string ExternalId, string? ParentExternalId, int Stock);

/// <summary>Reads orders in a shop's own format. Customer data in it is never read.</summary>
public interface IShopOrderReader
{
    /// <summary>The order in a webhook body; null when the body is not an order that can be read.</summary>
    ShopOrder? ReadWebhook(SalesChannelType type, byte[] body);
}

/// <param name="BaseUrl">This application's public address as shops reach it, e.g. "https://app.zalihe.rs".</param>
public record WebhookSettings(string BaseUrl)
{
    /// <summary>Where a WooCommerce shop sends its events; the path is the webhook controller's route.</summary>
    public string WooCommerceDeliveryUrl(Guid channelId) => $"{BaseUrl.TrimEnd('/')}/webhooks/woo/{channelId}";
}

/// <summary>
/// A product (or variation) as read from a shop, in plain values.
/// </summary>
/// <param name="ParentExternalId">The variable product's ID for a variation; null for a simple product.</param>
/// <param name="Name">For a variation: the product name with its options, e.g. "Majica basic – M, Siva".</param>
/// <param name="GroupName">For a variation: the variable product's name; null for a simple product.</param>
/// <param name="Price">Regular price; null when the shop has none.</param>
/// <param name="Cost">What the shop owner paid for it (WooCommerce "Cost of goods sold"); null when the shop has none.</param>
/// <param name="Stock">Stock quantity; null when the shop doesn't track stock for the product.</param>
public record ExternalProduct(
    string ExternalId,
    string? ParentExternalId,
    string Name,
    string? GroupName,
    string? Sku,
    string? Category,
    decimal? Price,
    decimal? Cost,
    decimal? Stock);

/// <summary>
/// An order as read from a shop (webhook or sync), with only what stock needs: no customer data.
/// </summary>
/// <param name="CreatedAt">When the order was placed; a product takes stock only for orders placed after it was linked.</param>
/// <param name="ModifiedAt">When the shop last changed the order; decides which of two events is newer.</param>
/// <param name="Lines">All lines the order has now; a line that is gone was removed from the order.</param>
public record ShopOrder(string ExternalId, string Status, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt, IReadOnlyList<ShopOrderLine> Lines);

/// <param name="ExternalProductId">The product's ID, or the variation's ID for a variation (as in <see cref="ExternalProduct.ExternalId"/>).</param>
/// <param name="Name">The product's name as the order shows it; null when the shop didn't send one.</param>
public record ShopOrderLine(string ExternalProductId, decimal Quantity, string? Name = null);

public record WooCommerceCredentials(string ConsumerKey, string ConsumerSecret);

/// <summary>Creates the client for a shop from its address and plain-text credentials.</summary>
public interface ISalesChannelFactory
{
    ISalesChannel Create(SalesChannelType type, string baseUrl, WooCommerceCredentials credentials);
}

/// <summary>
/// The shop could not be used. <see cref="Code"/> is an error code for the user, e.g.
/// "channel.unauthorized" (wrong or revoked keys) or "channel.unreachable".
/// </summary>
public class SalesChannelException(string code) : Exception(code)
{
    public const string Unreachable = "channel.unreachable";
    public const string Unauthorized = "channel.unauthorized";
    public const string NotWooCommerce = "channel.not_woocommerce";
    public const string UnexpectedResponse = "channel.unexpected_response";
    public const string TooManyProducts = "channel.too_many_products";

    public string Code { get; } = code;
}
