using Zalihe.Domain.Channels;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Orders;

/// <summary>
/// One order line as the shop sends it now, with the item it is linked to.
/// </summary>
/// <param name="ExternalProductId">The product's ID in the shop, or the variation's ID for a variation.</param>
/// <param name="ItemId">The linked item; null when the product isn't linked to any item.</param>
/// <param name="Name">The product's name in the shop, for showing the line; null when the shop didn't send it.</param>
public record OrderLineSnapshot(string ExternalProductId, Guid? ItemId, decimal Quantity, string? Name = null);

/// <summary>A change of an item's stock caused by an order. Negative takes from stock (sale), positive returns it.</summary>
public record OrderStockChange(Guid ItemId, decimal Quantity);

/// <summary>
/// An order from a shop. Only what stock needs is kept: ID, status, dates and lines with
/// quantities, never the customer (CLAUDE.md, SPEC 8).
/// <para>
/// Instead of handling each status transition, every event computes the stock the order
/// should hold right now and applies only the difference to what it already holds (SPEC 6).
/// Duplicated, late and changed events are therefore correct by construction.
/// </para>
/// </summary>
public class ExternalOrder : ITenantOwned
{
    public const int ExternalIdMaxLength = 64;
    public const int StatusMaxLength = 32;

    /// <summary>Statuses in which the ordered goods are taken from stock (SPEC 6).</summary>
    public static readonly IReadOnlySet<string> ActiveStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "processing", "on-hold", "completed",
    };

    private readonly List<ExternalOrderLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ChannelId { get; private set; }

    /// <summary>The order's ID in the shop, e.g. "1041".</summary>
    public string ExternalId { get; private set; }

    /// <summary>The shop's status code, e.g. "processing"; null until the first event is applied.</summary>
    public string? Status { get; private set; }

    /// <summary>When the shop last changed the order; older events are ignored.</summary>
    public DateTimeOffset? LastModifiedAt { get; private set; }

    /// <summary>When the customer placed the order in the shop.</summary>
    public DateTimeOffset PlacedAt { get; private set; }

    /// <summary>When the order first reached this application.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ExternalOrderLine> Lines => _lines;

    public ExternalOrder(SalesChannel channel, string externalId, DateTimeOffset placedAt, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        externalId = externalId.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(externalId.Length, ExternalIdMaxLength, nameof(externalId));

        Id = Guid.CreateVersion7();
        TenantId = channel.TenantId;
        ChannelId = channel.Id;
        ExternalId = externalId;
        PlacedAt = placedAt;
        CreatedAt = createdAt;
    }

    // For EF Core.
    private ExternalOrder()
    {
        ExternalId = null!;
    }

    /// <summary>
    /// Brings the order to the state the shop reports and returns the stock changes that follow.
    /// An event older than the last applied one changes nothing and returns no changes. Lines missing
    /// from <paramref name="lines"/> were removed from the order and give their stock back.
    /// </summary>
    /// <param name="lines">The order's lines now; the same product may appear more than once.</param>
    public IReadOnlyList<OrderStockChange> Apply(string status, DateTimeOffset modifiedAt, IReadOnlyList<OrderLineSnapshot> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        status = status.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(status.Length, StatusMaxLength, nameof(status));

        if (LastModifiedAt is { } last && modifiedAt < last)
        {
            return [];
        }

        Status = status;
        LastModifiedAt = modifiedAt;
        var isActive = ActiveStatuses.Contains(status);

        var current = lines
            .GroupBy(l => l.ExternalProductId.Trim(), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (
                    ItemId: g.Select(l => l.ItemId).FirstOrDefault(id => id is not null),
                    Quantity: g.Sum(l => l.Quantity),
                    Name: g.Select(l => l.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))),
                StringComparer.Ordinal);

        var changes = new List<OrderStockChange>();
        foreach (var line in _lines)
        {
            if (!current.Remove(line.ExternalProductId, out var now))
            {
                now = (null, 0, null);
            }

            line.Update(now.ItemId, now.Quantity, now.Name);
            AddChange(changes, line.Settle(isActive));
        }

        foreach (var (productId, now) in current)
        {
            var line = new ExternalOrderLine(this, productId, now.ItemId, now.Quantity, now.Name);
            _lines.Add(line);
            AddChange(changes, line.Settle(isActive));
        }

        return changes;
    }

    private static void AddChange(List<OrderStockChange> changes, OrderStockChange? change)
    {
        if (change is not null) changes.Add(change);
    }
}
