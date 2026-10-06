using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Channels;

/// <summary>
/// Links an item to a product in a shop: a simple product, or one variation of a variable product.
/// An item is linked to at most one product per shop, and a product to at most one item.
/// </summary>
public class ItemChannelMapping : ITenantOwned
{
    public const int ExternalIdMaxLength = 64;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid ChannelId { get; private set; }

    /// <summary>The product's (or variation's) ID in the shop.</summary>
    public string ExternalId { get; private set; }

    /// <summary>For a variation, the ID of its variable product; null for a simple product.</summary>
    public string? ParentExternalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public ItemChannelMapping(Item item, SalesChannel channel, string externalId, string? parentExternalId, DateTimeOffset createdAt)
    {
        if (item.TenantId != channel.TenantId) throw new InvalidOperationException("The item and the shop belong to different companies.");

        Id = Guid.CreateVersion7();
        TenantId = item.TenantId;
        ItemId = item.Id;
        ChannelId = channel.Id;
        ExternalId = Required(externalId, nameof(externalId));
        ParentExternalId = string.IsNullOrWhiteSpace(parentExternalId) ? null : Required(parentExternalId, nameof(parentExternalId));
        CreatedAt = createdAt;
    }

    // For EF Core.
    private ItemChannelMapping()
    {
        ExternalId = null!;
    }

    private static string Required(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        value = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, ExternalIdMaxLength, paramName);
        return value;
    }
}
