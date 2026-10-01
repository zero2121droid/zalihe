using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Items;

public enum ItemChangeKind
{
    Created,
    Updated,
    Deactivated,
    Activated,
}

/// <summary>
/// One changed field: API field name (e.g. "minStock") with old and new value in an invariant,
/// language-neutral form ("12.5", "kom"); null means empty. Clients format and translate them.
/// </summary>
public class ItemFieldChange
{
    public required string Field { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

/// <summary>
/// An entry in an item's change log: created, edited (with the changed fields), deactivated or
/// activated. Like stock movements, entries are only ever added, never changed or deleted.
/// </summary>
public class ItemChange : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ItemId { get; private set; }
    public ItemChangeKind Kind { get; private set; }
    public List<ItemFieldChange> Changes { get; private set; } = [];

    /// <summary>Who made the change; null when unknown (e.g. items created before the log existed).</summary>
    public Guid? UserId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private ItemChange(Item item, ItemChangeKind kind, IEnumerable<ItemFieldChange> changes, Guid? userId, DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = item.TenantId;
        ItemId = item.Id;
        Kind = kind;
        Changes = [.. changes];
        UserId = userId;
        OccurredAt = occurredAt;
    }

    // For EF Core.
    private ItemChange()
    {
    }

    public static ItemChange Created(Item item, Guid? userId, DateTimeOffset occurredAt) =>
        new(item, ItemChangeKind.Created, [], userId, occurredAt);

    /// <summary>Null when nothing changed, so saving the same data leaves no entry.</summary>
    public static ItemChange? Updated(Item item, IReadOnlyList<ItemFieldChange> changes, Guid? userId, DateTimeOffset occurredAt) =>
        changes.Count == 0 ? null : new(item, ItemChangeKind.Updated, changes, userId, occurredAt);

    public static ItemChange ActiveChanged(Item item, Guid? userId, DateTimeOffset occurredAt) =>
        new(item, item.IsActive ? ItemChangeKind.Activated : ItemChangeKind.Deactivated, [], userId, occurredAt);
}
