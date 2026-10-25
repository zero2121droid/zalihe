using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Application.Orders;
using Zalihe.Domain.Channels;

namespace Zalihe.Application.Channels;

/// <summary>A connected shop as clients see it; credentials are never included.</summary>
public record SalesChannelDto(
    Guid Id,
    SalesChannelType Type,
    string BaseUrl,
    SalesChannelStatus Status,
    string? LastErrorCode,
    DateTimeOffset? FailingSince,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt);

/// <summary>How a connected shop is doing, for its card.</summary>
/// <param name="LinkedProducts">Shop products linked to items.</param>
/// <param name="OrdersWithUnlinked">Orders with a product that is still not linked to any item (as the "unlinked" filter of Orders).</param>
/// <param name="LastOrderAt">When the last order arrived from the shop; null before the first.</param>
/// <param name="LastOrderNumber">That order's number in the shop.</param>
/// <param name="PendingStock">Products whose new stock the shop doesn't have yet.</param>
public record ChannelSummaryDto(int LinkedProducts, int OrdersWithUnlinked, DateTimeOffset? LastOrderAt, string? LastOrderNumber, int PendingStock);

/// <summary>A started shop connection: the user is sent to <paramref name="AuthorizeUrl"/>, the shop's approval page.</summary>
public record ShopAuthorizationDto(Guid Id, string AuthorizeUrl);

public record ShopAuthorizationResult(ShopAuthorizationDto? Authorization, IReadOnlyList<AppError> Errors);

public record ChannelResult(SalesChannelDto? Channel, IReadOnlyList<AppError> Errors, bool NotFound = false)
{
    public bool Succeeded => Channel is not null;

    public static ChannelResult Missing { get; } = new(null, [], NotFound: true);

    public static ChannelResult Failed(string code, string? field = null) => new(null, [new AppError(code, field)]);
}

/// <summary>
/// Connecting online shops. The connection is checked against the shop before anything is
/// saved, so a stored channel always had working credentials at the time it was saved. Every
/// check also makes sure the shop sends its orders to this application (webhooks).
/// </summary>
public class ChannelService(
    IAppDbContext db,
    ITenantContext tenant,
    ICredentialProtector protector,
    ISalesChannelFactory channels,
    WebhookSettings webhooks,
    ShopAuthorizationSettings authorizationSettings,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SalesChannelDto>> ListAsync(CancellationToken ct) =>
        (await db.SalesChannels.AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync(ct)).Select(ToDto).ToList();

    /// <summary>The shop's linked products, orders and stock waiting to be sent; null when there is no such shop.</summary>
    public async Task<ChannelSummaryDto?> GetSummaryAsync(Guid channelId, CancellationToken ct)
    {
        if (!await db.SalesChannels.AnyAsync(c => c.Id == channelId, ct))
        {
            return null;
        }

        var mappings = db.ItemChannelMappings.Where(m => m.ChannelId == channelId);
        var orders = db.ExternalOrders.Where(o => o.ChannelId == channelId);
        var lastOrder = await orders
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Select(o => new { o.CreatedAt, o.ExternalId })
            .FirstOrDefaultAsync(ct);

        return new ChannelSummaryDto(
            LinkedProducts: await mappings.CountAsync(ct),
            OrdersWithUnlinked: await orders.WithUnlinkedLines(db).CountAsync(ct),
            LastOrderAt: lastOrder?.CreatedAt,
            LastOrderNumber: lastOrder?.ExternalId,
            PendingStock: await mappings.CountAsync(m => m.StockChangedAt != null, ct));
    }

    /// <summary>Connects a WooCommerce shop with manually entered keys (one shop per company in v1).</summary>
    public async Task<ChannelResult> ConnectWooCommerceAsync(string baseUrl, string consumerKey, string consumerSecret, CancellationToken ct)
    {
        if (SalesChannel.NormalizeBaseUrl(baseUrl) is not { } url)
        {
            return ChannelResult.Failed("channel.url_invalid", "baseUrl");
        }

        if (ReadCredentials(consumerKey, consumerSecret) is not { } credentials)
        {
            return ChannelResult.Failed("channel.keys_invalid", "consumerKey");
        }

        return await ConnectAsync(url, credentials, authorization: null, ct);
    }

    /// <summary>
    /// Starts connecting a WooCommerce shop without copying keys (wc-auth, SPEC 7.1): the user approves
    /// access on the shop's page, the shop sends the keys to <see cref="ReceiveWooCommerceKeysAsync"/> and
    /// the user back to the application, which then calls <see cref="CompleteWooCommerceAuthorizationAsync"/>.
    /// </summary>
    public async Task<ShopAuthorizationResult> StartWooCommerceAuthorizationAsync(string baseUrl, CancellationToken ct)
    {
        if (SalesChannel.NormalizeBaseUrl(baseUrl) is not { } url)
        {
            return new(null, [new AppError("channel.url_invalid", "baseUrl")]);
        }

        if (await db.SalesChannels.AnyAsync(c => c.Type == SalesChannelType.WooCommerce, ct))
        {
            return new(null, [new AppError("channel.already_connected")]);
        }

        // Only the latest attempt counts; earlier ones, with any keys they received, are dropped.
        await db.ShopAuthorizations.Where(a => a.Type == SalesChannelType.WooCommerce).ExecuteDeleteAsync(ct);

        var token = ShopAuthorization.NewToken();
        var authorization = new ShopAuthorization(tenant.TenantId, SalesChannelType.WooCommerce, url, token, timeProvider.GetUtcNow());
        db.ShopAuthorizations.Add(authorization);
        await db.SaveChangesAsync(ct);

        // The token goes as WooCommerce's "user_id", which the shop sends back with the keys.
        var authorizeUrl = $"{url}/wc-auth/v1/authorize"
            + $"?app_name={Uri.EscapeDataString(ShopAuthorizationSettings.AppName)}"
            + "&scope=read_write"
            + $"&user_id={token}"
            + $"&return_url={Uri.EscapeDataString(authorizationSettings.ReturnUrl(authorization.Id))}"
            + $"&callback_url={Uri.EscapeDataString(authorizationSettings.WooCommerceCallbackUrl)}";
        return new(new ShopAuthorizationDto(authorization.Id, authorizeUrl), []);
    }

    /// <summary>
    /// Keeps the keys a shop sent after its owner approved access. The current company must be the
    /// authorization's (see <see cref="IAppDbContext.FindShopAuthorizationTenantAsync"/>).
    /// </summary>
    /// <returns>
    /// False when the token is unknown, the authorization expired or already has keys, or the keys don't
    /// look like WooCommerce keys. The shop then shows its owner an error and deletes the keys.
    /// </returns>
    public async Task<bool> ReceiveWooCommerceKeysAsync(string token, string consumerKey, string consumerSecret, CancellationToken ct)
    {
        var hash = ShopAuthorization.HashToken(token);
        var authorization = await db.ShopAuthorizations.SingleOrDefaultAsync(a => a.TokenHash == hash, ct);
        var now = timeProvider.GetUtcNow();
        if (authorization is null || !authorization.CanReceiveKeysAt(now) || ReadCredentials(consumerKey, consumerSecret) is not { } credentials)
        {
            return false;
        }

        authorization.ReceiveKeys(protector.Protect(JsonSerializer.Serialize(credentials)), now);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Saves the shop whose owner approved access, once its keys work, as with keys entered by hand.
    /// When the shop rejects them the authorization stays, so the user can try again.
    /// </summary>
    public async Task<ChannelResult> CompleteWooCommerceAuthorizationAsync(Guid authorizationId, CancellationToken ct)
    {
        var authorization = await db.ShopAuthorizations.SingleOrDefaultAsync(a => a.Id == authorizationId, ct);
        if (authorization is null)
        {
            return ChannelResult.Missing;
        }

        if (authorization.EncryptedCredentials is not { } encrypted)
        {
            return ChannelResult.Failed(timeProvider.GetUtcNow() >= authorization.ExpiresAt
                ? "channel.authorization_expired"
                : "channel.authorization_pending");
        }

        var credentials = JsonSerializer.Deserialize<WooCommerceCredentials>(protector.Unprotect(encrypted))!;
        return await ConnectAsync(authorization.BaseUrl, credentials, authorization, ct);
    }

    /// <summary>Saves the shop as a channel if the keys work; a used <paramref name="authorization"/> goes in the same save.</summary>
    private async Task<ChannelResult> ConnectAsync(string url, WooCommerceCredentials credentials, ShopAuthorization? authorization, CancellationToken ct)
    {
        if (await db.SalesChannels.AnyAsync(c => c.Type == SalesChannelType.WooCommerce, ct))
        {
            return ChannelResult.Failed("channel.already_connected");
        }

        // Created before the shop is called, because the webhooks point to the channel's ID; saved only if the shop accepts.
        var channel = new SalesChannel(tenant.TenantId, SalesChannelType.WooCommerce, url,
            protector.Protect(JsonSerializer.Serialize(credentials)),
            protector.Protect(NewWebhookSecret()),
            timeProvider.GetUtcNow());

        if (await SetUpShopAsync(channel, credentials, ct) is { } error)
        {
            return ChannelResult.Failed(error);
        }

        db.SalesChannels.Add(channel);
        if (authorization is not null) db.ShopAuthorizations.Remove(authorization);
        await db.SaveChangesAsync(ct);
        return new ChannelResult(ToDto(channel), []);
    }

    /// <summary>Contacts the shop again with the stored credentials and records the outcome.</summary>
    public async Task<ChannelResult> RecheckAsync(Guid channelId, CancellationToken ct)
    {
        var channel = await db.SalesChannels.SingleOrDefaultAsync(c => c.Id == channelId, ct);
        if (channel is null)
        {
            return ChannelResult.Missing;
        }

        var credentials = JsonSerializer.Deserialize<WooCommerceCredentials>(protector.Unprotect(channel.EncryptedCredentials))!;
        if (await SetUpShopAsync(channel, credentials, ct) is { } error) channel.MarkFailed(error, timeProvider.GetUtcNow());
        else channel.MarkConnected();

        await db.SaveChangesAsync(ct);
        return new ChannelResult(ToDto(channel), []);
    }

    /// <summary>Replaces the keys (e.g. after they were revoked); the new keys must work first.</summary>
    public async Task<ChannelResult> ReplaceCredentialsAsync(Guid channelId, string consumerKey, string consumerSecret, CancellationToken ct)
    {
        var channel = await db.SalesChannels.SingleOrDefaultAsync(c => c.Id == channelId, ct);
        if (channel is null)
        {
            return ChannelResult.Missing;
        }

        if (ReadCredentials(consumerKey, consumerSecret) is not { } credentials)
        {
            return ChannelResult.Failed("channel.keys_invalid", "consumerKey");
        }

        if (await SetUpShopAsync(channel, credentials, ct) is { } error)
        {
            return ChannelResult.Failed(error);
        }

        channel.ReplaceCredentials(protector.Protect(JsonSerializer.Serialize(credentials)));
        await db.SaveChangesAsync(ct);
        return new ChannelResult(ToDto(channel), []);
    }

    /// <summary>WooCommerce keys look like "ck_…" and "cs_…"; catches swapped or half-copied keys early.</summary>
    private static WooCommerceCredentials? ReadCredentials(string consumerKey, string consumerSecret)
    {
        var key = consumerKey.Trim();
        var secret = consumerSecret.Trim();
        return key.StartsWith("ck_", StringComparison.Ordinal) && secret.StartsWith("cs_", StringComparison.Ordinal)
            && key.Length > 10 && secret.Length > 10
            ? new WooCommerceCredentials(key, secret)
            : null;
    }

    /// <summary>
    /// Checks the credentials, then makes sure the shop sends its orders here. Returns the shop's
    /// error code, or null when both worked. Read-only keys pass the check but can't create webhooks.
    /// </summary>
    private async Task<string?> SetUpShopAsync(SalesChannel channel, WooCommerceCredentials credentials, CancellationToken ct)
    {
        try
        {
            var shop = channels.Create(channel.Type, channel.BaseUrl, credentials);
            await shop.CheckConnectionAsync(ct);
            await shop.EnsureWebhooksAsync(webhooks.WooCommerceDeliveryUrl(channel.Id),
                protector.Unprotect(channel.EncryptedWebhookSecret), ct);
            return null;
        }
        catch (SalesChannelException e)
        {
            return e.Code;
        }
    }

    private static string NewWebhookSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static SalesChannelDto ToDto(SalesChannel c) =>
        new(c.Id, c.Type, c.BaseUrl, c.Status, c.LastErrorCode, c.FailingSince, c.LastSyncedAt, c.CreatedAt);
}
