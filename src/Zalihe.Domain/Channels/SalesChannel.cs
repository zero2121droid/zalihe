using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Channels;

public enum SalesChannelType
{
    WooCommerce,
}

public enum SalesChannelStatus
{
    Connected,

    /// <summary>The last contact with the shop failed; <see cref="SalesChannel.LastErrorCode"/> says why.</summary>
    Error,
}

/// <summary>
/// A connected online shop. Credentials are stored only in encrypted form (ASP.NET Data
/// Protection); the domain never sees them in plain text.
/// </summary>
public class SalesChannel : ITenantOwned
{
    public const int BaseUrlMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public SalesChannelType Type { get; private set; }

    /// <summary>Shop address without a trailing slash, e.g. "https://zrno.rs".</summary>
    public string BaseUrl { get; private set; }

    public string EncryptedCredentials { get; private set; }

    /// <summary>Secret for verifying webhook signatures, encrypted like the credentials.</summary>
    public string EncryptedWebhookSecret { get; private set; }

    public SalesChannelStatus Status { get; private set; }

    /// <summary>
    /// How long the shop may keep failing in the background before the channel shows an error: one
    /// failed sync or push is often a passing hiccup (a restart, a slow host), not a reason to alarm the owner.
    /// </summary>
    public static readonly TimeSpan ErrorAfter = TimeSpan.FromMinutes(30);

    /// <summary>Error code of the last failed contact (e.g. "channel.unauthorized"); null when the last contact worked.</summary>
    public string? LastErrorCode { get; private set; }

    /// <summary>When the shop started failing, counting from the first of the failed contacts in a row; null when the last contact worked.</summary>
    public DateTimeOffset? FailingSince { get; private set; }

    /// <summary>
    /// When the owner was emailed that the shop stopped working; null when not (yet) for the current
    /// failure. One email per failure: it is cleared once the shop works again.
    /// </summary>
    public DateTimeOffset? ErrorEmailedAt { get; private set; }

    /// <summary>When the last order sync started; the next one asks the shop for orders changed since then.</summary>
    public DateTimeOffset? LastSyncedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public SalesChannel(Guid tenantId, SalesChannelType type, string baseUrl, string encryptedCredentials,
        string encryptedWebhookSecret, DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedWebhookSecret);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Type = type;
        BaseUrl = NormalizeBaseUrl(baseUrl) ?? throw new ArgumentException("Invalid shop address.", nameof(baseUrl));
        EncryptedCredentials = encryptedCredentials;
        EncryptedWebhookSecret = encryptedWebhookSecret;
        Status = SalesChannelStatus.Connected;
        CreatedAt = createdAt;
    }

    // For EF Core.
    private SalesChannel()
    {
        BaseUrl = null!;
        EncryptedCredentials = null!;
        EncryptedWebhookSecret = null!;
    }

    public void ReplaceCredentials(string encryptedCredentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);
        EncryptedCredentials = encryptedCredentials;
        MarkConnected();
    }

    public void MarkConnected()
    {
        Status = SalesChannelStatus.Connected;
        LastErrorCode = null;
        FailingSince = null;
        ErrorEmailedAt = null;
    }

    /// <param name="startedAt">When the sync started, so orders changed while it ran are read again next time.</param>
    public void MarkSynced(DateTimeOffset startedAt)
    {
        if (LastSyncedAt is { } last && startedAt < last) return; // an older run finished after a newer one
        LastSyncedAt = startedAt;
    }

    /// <summary>A check the user asked for failed: the channel shows the error right away.</summary>
    public void MarkFailed(string errorCode, DateTimeOffset at)
    {
        RecordFailure(errorCode, at);
        Status = SalesChannelStatus.Error;
    }

    /// <summary>
    /// A background contact (sync, stock push) failed. The channel shows an error once the shop has
    /// been failing for <see cref="ErrorAfter"/>; any contact that works brings it back (<see cref="MarkConnected"/>).
    /// </summary>
    /// <returns>True when this failure is the one that put the channel into error: time to tell the owner.</returns>
    public bool RecordFailure(string errorCode, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        LastErrorCode = errorCode;
        FailingSince ??= at;
        if (Status == SalesChannelStatus.Error || at - FailingSince < ErrorAfter) return false;

        Status = SalesChannelStatus.Error;
        return true;
    }

    /// <summary>The shop isn't working and the owner hasn't been emailed about it yet.</summary>
    public bool NeedsErrorEmail => Status == SalesChannelStatus.Error && ErrorEmailedAt is null;

    public void MarkErrorEmailed(DateTimeOffset at) => ErrorEmailedAt = at;

    /// <summary>
    /// "zrno.rs/" → "https://zrno.rs". Only https is accepted, except plain http for the local
    /// machine (the development test shop). Null when the address is not usable.
    /// </summary>
    public static string? NormalizeBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return null;
        var isLocal = uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && isLocal)) return null;
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return null;

        var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return normalized.Length <= BaseUrlMaxLength ? normalized : null;
    }
}
