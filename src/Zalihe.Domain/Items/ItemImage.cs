using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Items;

/// <summary>
/// A picture the user added to an item. It takes precedence over the shop's product image.
/// Pictures are small (the browser shrinks them before upload), so they are kept in the database:
/// the tenant filter protects them like any other data, and the database backup covers them.
/// </summary>
public class ItemImage : ITenantOwned
{
    /// <summary>Larger than any picture the web app sends after shrinking it, small enough for a database row.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    public Guid ItemId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>"image/jpeg", "image/png" or "image/webp", from the file's content, not from what the client said.</summary>
    public string ContentType { get; private set; }

    public byte[] Data { get; private set; }

    /// <summary>When the picture was last set; part of its address, so a new picture is never served from cache.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <exception cref="ArgumentException">The data is not a JPEG, PNG or WebP picture, or is too large.</exception>
    public ItemImage(Item item, byte[] data, DateTimeOffset updatedAt)
    {
        ItemId = item.Id;
        TenantId = item.TenantId;
        ContentType = null!;
        Data = null!;
        Replace(data, updatedAt);
    }

    /// <exception cref="ArgumentException">The data is not a JPEG, PNG or WebP picture, or is too large.</exception>
    public void Replace(byte[] data, DateTimeOffset updatedAt)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(data.Length, MaxBytes, nameof(data));
        ContentType = DetectContentType(data) ?? throw new ArgumentException("Not a JPEG, PNG or WebP picture.", nameof(data));
        Data = data;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// The picture's type from its first bytes ("magic numbers"); null for anything else. SVG is
    /// never accepted: it can carry scripts.
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> data) =>
        data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) ? "image/jpeg"
        : data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ? "image/png"
        : data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8) ? "image/webp"
        : null;

    // For EF Core.
    private ItemImage()
    {
        ContentType = null!;
        Data = null!;
    }
}
