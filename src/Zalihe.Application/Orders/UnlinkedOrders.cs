using Zalihe.Application.Common;
using Zalihe.Domain.Orders;

namespace Zalihe.Application.Orders;

public static class UnlinkedOrders
{
    /// <summary>
    /// Orders with at least one <see cref="OrderLineState.Unlinked"/> line: a product that took no stock
    /// and is still not linked to any item, so the stock is off until it is linked. One rule for the
    /// "unlinked" filter of Orders, the shop card and the home screen.
    /// </summary>
    public static IQueryable<ExternalOrder> WithUnlinkedLines(this IQueryable<ExternalOrder> orders, IAppDbContext db) =>
        orders.Where(o => o.Lines.Any(l =>
            l.ItemId == null && l.OrderedQuantity > 0
            && !db.ItemChannelMappings.Any(m => m.ChannelId == o.ChannelId && m.ExternalId == l.ExternalProductId)));
}
