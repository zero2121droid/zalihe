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
}

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

    public string Code { get; } = code;
}
