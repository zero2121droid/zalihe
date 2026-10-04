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

    /// <summary>Error code of the last failed contact (e.g. "channel.unauthorized"); null when connected.</summary>
    public string? LastErrorCode { get; private set; }

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
    }

    public void MarkFailed(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        Status = SalesChannelStatus.Error;
        LastErrorCode = errorCode;
    }

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
