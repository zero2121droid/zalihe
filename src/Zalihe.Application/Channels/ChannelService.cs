using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Channels;

namespace Zalihe.Application.Channels;

/// <summary>A connected shop as clients see it; credentials are never included.</summary>
public record SalesChannelDto(
    Guid Id,
    SalesChannelType Type,
    string BaseUrl,
    SalesChannelStatus Status,
    string? LastErrorCode,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt);

public record ChannelResult(SalesChannelDto? Channel, IReadOnlyList<AppError> Errors, bool NotFound = false)
{
    public bool Succeeded => Channel is not null;

    public static ChannelResult Missing { get; } = new(null, [], NotFound: true);

    public static ChannelResult Failed(string code, string? field = null) => new(null, [new AppError(code, field)]);
}

/// <summary>
/// Connecting online shops. The connection is checked against the shop before anything is
/// saved, so a stored channel always had working credentials at the time it was saved.
/// </summary>
public class ChannelService(
    IAppDbContext db,
    ITenantContext tenant,
    ICredentialProtector protector,
    ISalesChannelFactory channels,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SalesChannelDto>> ListAsync(CancellationToken ct) =>
        (await db.SalesChannels.AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync(ct)).Select(ToDto).ToList();

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

        if (await db.SalesChannels.AnyAsync(c => c.Type == SalesChannelType.WooCommerce, ct))
        {
            return ChannelResult.Failed("channel.already_connected");
        }

        if (await CheckAsync(url, credentials, ct) is { } error)
        {
            return ChannelResult.Failed(error);
        }

        var channel = new SalesChannel(tenant.TenantId, SalesChannelType.WooCommerce, url,
            protector.Protect(JsonSerializer.Serialize(credentials)),
            protector.Protect(NewWebhookSecret()),
            timeProvider.GetUtcNow());
        db.SalesChannels.Add(channel);
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
        if (await CheckAsync(channel.BaseUrl, credentials, ct) is { } error) channel.MarkFailed(error);
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

        if (await CheckAsync(channel.BaseUrl, credentials, ct) is { } error)
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

    private async Task<string?> CheckAsync(string baseUrl, WooCommerceCredentials credentials, CancellationToken ct)
    {
        try
        {
            await channels.Create(SalesChannelType.WooCommerce, baseUrl, credentials).CheckConnectionAsync(ct);
            return null;
        }
        catch (SalesChannelException e)
        {
            return e.Code;
        }
    }

    private static string NewWebhookSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static SalesChannelDto ToDto(SalesChannel c) =>
        new(c.Id, c.Type, c.BaseUrl, c.Status, c.LastErrorCode, c.LastSyncedAt, c.CreatedAt);
}
