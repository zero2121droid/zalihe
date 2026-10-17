using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;
using Zalihe.Domain.Orders;

namespace Zalihe.Application.Orders;

/// <param name="Number">The order's number in the shop, e.g. "1041".</param>
/// <param name="ShopUrl">The shop's address, to tell orders of several shops apart.</param>
/// <param name="Status">The shop's status code, e.g. "processing"; the frontend translates the known ones.</param>
/// <param name="TakesStock">Whether the order holds its goods from stock in this status (SPEC 6).</param>
/// <param name="UnlinkedLineCount">Lines whose product isn't linked to any item; see <see cref="OrderLineState.Unlinked"/>.</param>
/// <param name="Lines">The products the order holds now; lines removed from it are left out.</param>
public record OrderDto(
    Guid Id,
    string Number,
    Guid ChannelId,
    string ShopUrl,
    string? Status,
    bool TakesStock,
    DateTimeOffset PlacedAt,
    int UnlinkedLineCount,
    IReadOnlyList<OrderLineDto> Lines);

/// <param name="Name">The product's name in the shop; null for orders that arrived before names were kept.</param>
/// <param name="Item">The item the line takes stock from, or for <see cref="OrderLineState.LinkedAfterOrder"/> the one the product is linked to now; null when unlinked.</param>
/// <param name="AppliedQuantity">How much the line has taken from the item's stock; 0 while the order doesn't take stock.</param>
public record OrderLineDto(
    string ExternalProductId,
    string? Name,
    OrderLineState State,
    OrderLineItemDto? Item,
    decimal OrderedQuantity,
    decimal AppliedQuantity);

public record OrderLineItemDto(Guid Id, string Name, string Sku, Unit Unit);

public enum OrderLineState
{
    /// <summary>The product is linked to an item, and the line takes stock from it.</summary>
    Linked,

    /// <summary>The product isn't linked to any item, so the line takes no stock; the user should link it.</summary>
    Unlinked,

    /// <summary>
    /// The product was linked after the order was placed, so the line takes no stock: the item's stock
    /// at linking already excluded the order (see OrderService). Nothing for the user to do.
    /// </summary>
    LinkedAfterOrder,
}

/// <param name="Search">Order number, or part of a product's name.</param>
/// <param name="Status">Only orders in this shop status, e.g. "processing".</param>
/// <param name="UnlinkedOnly">Only orders with at least one <see cref="OrderLineState.Unlinked"/> line.</param>
public record OrderListQuery(
    string? Search = null,
    string? Status = null,
    bool UnlinkedOnly = false,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);

/// <summary>
/// Orders that came from the company's shops, newest first, so the user can see what took stock.
/// Only what is stored is shown: there is no customer data (SPEC 8).
/// </summary>
public class OrderListService(IAppDbContext db)
{
    public async Task<PagedResult<OrderDto>> ListAsync(OrderListQuery query, CancellationToken ct)
    {
        var orders = db.ExternalOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().TrimStart('#').ToLower();
            orders = orders.Where(o =>
                o.ExternalId.ToLower().Contains(term)
                || o.Lines.Any(l => l.OrderedQuantity > 0 && l.Name != null && l.Name.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            orders = orders.Where(o => o.Status == status);
        }

        if (query.UnlinkedOnly)
        {
            orders = orders.Where(o => o.Lines.Any(l =>
                l.ItemId == null && l.OrderedQuantity > 0
                && !db.ItemChannelMappings.Any(m => m.ChannelId == o.ChannelId && m.ExternalId == l.ExternalProductId)));
        }

        var totalCount = await orders.CountAsync(ct);
        var page = await orders
            .OrderByDescending(o => o.PlacedAt)
            .ThenByDescending(o => o.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(o => o.Lines)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(await ToDtosAsync(page, ct), totalCount, query.Page, query.PageSize);
    }

    private async Task<IReadOnlyList<OrderDto>> ToDtosAsync(IReadOnlyList<ExternalOrder> orders, CancellationToken ct)
    {
        var channelIds = orders.Select(o => o.ChannelId).Distinct().ToList();
        var shops = await db.SalesChannels.AsNoTracking()
            .Where(c => channelIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.BaseUrl, ct);

        // Products linked now, for lines that took no stock: unlinked, or linked after the order.
        var notTaking = orders.SelectMany(o => o.Lines).Where(l => l.ItemId is null).Select(l => l.ExternalProductId).Distinct().ToList();
        var mappings = await db.ItemChannelMappings.AsNoTracking()
            .Where(m => channelIds.Contains(m.ChannelId) && notTaking.Contains(m.ExternalId))
            .ToDictionaryAsync(m => (m.ChannelId, m.ExternalId), m => m.ItemId, ct);

        var itemIds = orders.SelectMany(o => o.Lines).Select(l => l.ItemId).OfType<Guid>().Concat(mappings.Values).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => new OrderLineItemDto(i.Id, i.Name, i.Sku, i.Unit), ct);

        return orders.Select(order =>
        {
            var lines = order.Lines
                .Where(l => l.OrderedQuantity > 0)
                .OrderBy(l => l.Id)
                .Select(line =>
                {
                    Guid? itemId = line.ItemId ?? (mappings.TryGetValue((order.ChannelId, line.ExternalProductId), out var linkedNow) ? linkedNow : null);
                    var state = line.ItemId is not null ? OrderLineState.Linked
                        : itemId is not null ? OrderLineState.LinkedAfterOrder
                        : OrderLineState.Unlinked;
                    var item = itemId is { } known ? items.GetValueOrDefault(known) : null;
                    return new OrderLineDto(line.ExternalProductId, line.Name, state, item, line.OrderedQuantity, line.AppliedQuantity);
                })
                .ToList();

            return new OrderDto(
                order.Id,
                order.ExternalId,
                order.ChannelId,
                shops.GetValueOrDefault(order.ChannelId, ""),
                order.Status,
                order.Status is { } status && ExternalOrder.ActiveStatuses.Contains(status),
                order.PlacedAt,
                lines.Count(l => l.State == OrderLineState.Unlinked),
                lines);
        }).ToList();
    }
}
