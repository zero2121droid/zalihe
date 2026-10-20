using System.Security.Cryptography;
using System.Text;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Users;

public enum InvitationState
{
    Pending,
    Accepted,
    Revoked,
    Expired,
}

/// <summary>
/// An invitation to join a company (SPEC 2.7). The link carries a random token; only its hash is
/// stored, so a database leak gives no working links. A token is used once, and a new invitation to
/// the same email replaces the old one (see <see cref="Renew"/>).
/// </summary>
public class Invitation : ITenantOwned
{
    public const int EmailMaxLength = 256;
    public static readonly TimeSpan ValidFor = TimeSpan.FromDays(7);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Trimmed and lowercase, so the same address is the same invitation.</summary>
    public string Email { get; private set; }

    /// <summary>SHA-256 of the token, hex.</summary>
    public string TokenHash { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    /// <summary>When it was (last) sent; a renewed invitation counts from then.</summary>
    public DateTimeOffset SentAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <param name="token">The secret from <see cref="NewToken"/>, sent in the link and never stored.</param>
    public Invitation(Guid tenantId, string email, string token, Guid invitedByUserId, DateTimeOffset sentAt)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Email = NormalizeEmail(email);
        TokenHash = null!;
        Renew(token, invitedByUserId, sentAt);
    }

    // For EF Core.
    private Invitation()
    {
        Email = null!;
        TokenHash = null!;
    }

    public InvitationState StateAt(DateTimeOffset now) =>
        AcceptedAt is not null ? InvitationState.Accepted
        : RevokedAt is not null ? InvitationState.Revoked
        : now >= ExpiresAt ? InvitationState.Expired
        : InvitationState.Pending;

    /// <summary>Sends the invitation again with a new token: the old link stops working, and the week starts over.</summary>
    public void Renew(string token, Guid invitedByUserId, DateTimeOffset sentAt)
    {
        if (AcceptedAt is not null) throw new InvalidOperationException("The invitation was already accepted.");
        TokenHash = HashToken(token);
        InvitedByUserId = invitedByUserId;
        SentAt = sentAt;
        ExpiresAt = sentAt + ValidFor;
        RevokedAt = null;
    }

    /// <exception cref="InvalidOperationException">The invitation is no longer pending.</exception>
    public void Accept(DateTimeOffset at)
    {
        if (StateAt(at) != InvitationState.Pending) throw new InvalidOperationException("The invitation can't be accepted.");
        AcceptedAt = at;
    }

    /// <summary>Withdraws a pending invitation; its link stops working.</summary>
    public void Revoke(DateTimeOffset at)
    {
        if (AcceptedAt is null) RevokedAt ??= at;
    }

    /// <summary>A random, unguessable token for the link (256 bits, URL-safe).</summary>
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        email = email.Trim().ToLowerInvariant();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(email.Length, EmailMaxLength, nameof(email));
        return email;
    }
}
