using System.Security.Cryptography;
using System.Text;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Channels;

/// <summary>
/// A shop connection the user started by sending the shop owner to approve access (WooCommerce
/// wc-auth, SPEC 7.1). The shop sends the keys straight to this application, marked with a random
/// token that only the shop and the user's browser see; only its hash is stored. The keys are kept
/// encrypted until the user comes back and the connection is checked and saved as a channel.
/// </summary>
public class ShopAuthorization : ITenantOwned
{
    /// <summary>Time to sign in to the shop and approve; the shop's keys are accepted only within it.</summary>
    public static readonly TimeSpan ValidFor = TimeSpan.FromHours(1);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public SalesChannelType Type { get; private set; }

    /// <summary>Normalized like <see cref="SalesChannel.BaseUrl"/>.</summary>
    public string BaseUrl { get; private set; }

    /// <summary>SHA-256 of the token, hex.</summary>
    public string TokenHash { get; private set; }

    /// <summary>The keys the shop sent, encrypted like a channel's; null until they arrive.</summary>
    public string? EncryptedCredentials { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? KeysReceivedAt { get; private set; }

    /// <param name="token">The secret from <see cref="NewToken"/>, given to the shop and never stored.</param>
    public ShopAuthorization(Guid tenantId, SalesChannelType type, string baseUrl, string token, DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Type = type;
        BaseUrl = SalesChannel.NormalizeBaseUrl(baseUrl) ?? throw new ArgumentException("Invalid shop address.", nameof(baseUrl));
        TokenHash = HashToken(token);
        CreatedAt = createdAt;
        ExpiresAt = createdAt + ValidFor;
    }

    // For EF Core.
    private ShopAuthorization()
    {
        BaseUrl = null!;
        TokenHash = null!;
    }

    public bool HasKeys => EncryptedCredentials is not null;

    /// <summary>The shop may still send keys: once, and only before the authorization expires.</summary>
    public bool CanReceiveKeysAt(DateTimeOffset now) => !HasKeys && now < ExpiresAt;

    /// <exception cref="InvalidOperationException">Keys were already received, or the authorization expired.</exception>
    public void ReceiveKeys(string encryptedCredentials, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);
        if (!CanReceiveKeysAt(at)) throw new InvalidOperationException("The authorization can't receive keys.");
        EncryptedCredentials = encryptedCredentials;
        KeysReceivedAt = at;
    }

    /// <summary>A random, unguessable token (256 bits, URL-safe).</summary>
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
