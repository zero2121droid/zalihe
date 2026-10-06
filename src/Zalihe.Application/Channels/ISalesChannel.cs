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
}

/// <summary>
/// A product (or variation) as read from a shop, in plain values.
/// </summary>
/// <param name="ParentExternalId">The variable product's ID for a variation; null for a simple product.</param>
/// <param name="Name">For a variation: the product name with its options, e.g. "Majica basic – M, Siva".</param>
/// <param name="GroupName">For a variation: the variable product's name; null for a simple product.</param>
/// <param name="Price">Regular price; null when the shop has none.</param>
/// <param name="Stock">Stock quantity; null when the shop doesn't track stock for the product.</param>
public record ExternalProduct(
    string ExternalId,
    string? ParentExternalId,
    string Name,
    string? GroupName,
    string? Sku,
    string? Category,
    decimal? Price,
    decimal? Stock);

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
