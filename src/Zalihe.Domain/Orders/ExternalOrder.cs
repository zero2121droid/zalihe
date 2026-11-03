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

/// <summary>An item and how much of it a manual order holds.</summary>
public record ManualOrderLine(Guid ItemId, decimal Quantity);

/// <summary>Where an order came from (SPEC 11.5).</summary>
public enum OrderSource
{
    /// <summary>From a connected shop, by webhook or sync; only the shop changes it.</summary>
    WooCommerce,

    /// <summary>Entered in Lagerko for a sale outside the shop (Instagram, phone, store); exists only here.</summary>
    Manual,
}

/// <summary>Statuses of a manual order. Cancelled is the shop's own code, so both kinds share its translation.</summary>
public static class ManualOrderStatus
{
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Confirmed, Cancelled };
}

/// <summary>
/// An order from a shop, or a manual one entered in Lagerko (SPEC 11.5). Only what stock needs is
/// kept: ID, status, dates and lines with quantities, never the customer (CLAUDE.md, SPEC 8).
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
    public const int ReferenceMaxLength = 64;
    public const int NoteMaxLength = 500;

    /// <summary>Shop statuses in which the ordered goods are taken from stock (SPEC 6).</summary>
    public static readonly IReadOnlySet<string> ActiveStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "processing", "on-hold", "completed",
    };

    /// <summary>Manual order statuses in which the goods are taken from stock (SPEC 11.5).</summary>
    public static readonly IReadOnlySet<string> ManualActiveStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        ManualOrderStatus.Confirmed,
    };

    private readonly List<ExternalOrderLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public OrderSource Source { get; private set; }

    /// <summary>The shop the order came from; null for a manual order.</summary>
    public Guid? ChannelId { get; private set; }

    /// <summary>The order's ID in the shop, e.g. "1041", or a manual order's number, e.g. "R-0001".</summary>
    public string ExternalId { get; private set; }

    /// <summary>A manual order's own reference, e.g. "IG 154"; null for shop orders.</summary>
    public string? Reference { get; private set; }

    /// <summary>A manual order's free note; never the customer's address or phone (SPEC 11.5).</summary>
    public string? Note { get; private set; }

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
        Source = OrderSource.WooCommerce;
        ChannelId = channel.Id;
        ExternalId = externalId;
        PlacedAt = placedAt;
        CreatedAt = createdAt;
    }

    private ExternalOrder(Guid tenantId, string number, string? reference, string? note, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Source = OrderSource.Manual;
        ExternalId = number;
        Reference = Optional(reference, ReferenceMaxLength, nameof(reference));
        Note = Optional(note, NoteMaxLength, nameof(note));
        PlacedAt = now;
        CreatedAt = now;
    }

    /// <summary>
    /// A confirmed manual order holding <paramref name="lines"/>; the returned changes take them from stock.
    /// </summary>
    /// <param name="number">The company's next manual order number, starting at 1; shown as "R-0001".</param>
    /// <param name="lines">At least one line; the same item may appear more than once.</param>
    public static (ExternalOrder Order, IReadOnlyList<OrderStockChange> Changes) CreateManual(
        Guid tenantId, int number, IReadOnlyList<ManualOrderLine> lines, string? reference, string? note, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        if (lines.Count == 0) throw new ArgumentException("A manual order needs at least one line.", nameof(lines));
        foreach (var line in lines) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(line.Quantity, nameof(lines));

        var order = new ExternalOrder(tenantId, ManualNumber(number), reference, note, now);
        var changes = order.Apply(ManualOrderStatus.Confirmed, now, lines.Select(l => ManualSnapshot(l.ItemId, l.Quantity)).ToList());
        return (order, changes);
    }

    /// <summary>"R-0001" for 1; more digits once the company passes 9999.</summary>
    public static string ManualNumber(int number) => $"R-{number:D4}";

    /// <summary>Whether the order takes its goods from stock in its current status.</summary>
    public bool TakesStock => Status is { } status && IsActive(status);

    /// <summary>
    /// Changes a manual order's status and returns the stock changes that follow: cancelling gives
    /// everything back, confirming again takes it again. The same status twice changes nothing.
    /// </summary>
    public IReadOnlyList<OrderStockChange> SetManualStatus(string status, DateTimeOffset now)
    {
        if (Source != OrderSource.Manual) throw new InvalidOperationException("Only a manual order's status can be changed here.");
        if (!ManualOrderStatus.All.Contains(status)) throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown manual order status.");

        // A manual line's product is its item, so the lines it holds now describe it fully.
        var lines = _lines
            .Where(l => l.OrderedQuantity > 0)
            .Select(l => new OrderLineSnapshot(l.ExternalProductId, l.ItemId, l.OrderedQuantity))
            .ToList();
        // The user's clock only moves forward here, but a status change must never be ignored as older.
        return Apply(status, LastModifiedAt is { } last && now < last ? last : now, lines);
    }

    /// <summary>A manual line is keyed by its item, so the same item twice is one line.</summary>
    private static OrderLineSnapshot ManualSnapshot(Guid itemId, decimal quantity) => new(itemId.ToString(), itemId, quantity);

    private bool IsActive(string status) =>
        (Source == OrderSource.Manual ? ManualActiveStatuses : ActiveStatuses).Contains(status);

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
        var isActive = IsActive(status);

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

    private static string? Optional(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, maxLength, paramName);
        return value;
    }
}
