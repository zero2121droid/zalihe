using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;
using Zalihe.Domain.Orders;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Orders;

/// <param name="Lines">Items and quantities; the same item may appear more than once.</param>
/// <param name="Reference">Optional, e.g. "IG 154".</param>
/// <param name="Note">Optional; never the customer's address or phone (SPEC 11.5).</param>
public record CreateManualOrderCommand(IReadOnlyList<ManualOrderLine> Lines, string? Reference, string? Note);

/// <summary>What the user can set a manual order to (SPEC 11.5, first step).</summary>
public enum ManualOrderStatusChange
{
    Confirmed,
    Cancelled,
}

public record ManualOrderResult(OrderDto? Order, IReadOnlyList<AppError> Errors, bool NotFound = false)
{
    public bool Succeeded => Order is not null;

    public static ManualOrderResult Missing { get; } = new(null, [], NotFound: true);

    public static ManualOrderResult Failed(AppError error) => new(null, [error]);
}

/// <summary>
/// Orders entered in Lagerko for sales outside the shop (Instagram, phone, store; SPEC 11.5). They exist
/// only here and are never sent to the shop; only the new stock is, like after any other movement.
/// Stock is computed the same way as for shop orders: the order says what it holds now, and only the
/// difference is recorded.
/// </summary>
public class ManualOrderService(
    IAppDbContext db, OrderListService orderList, ITenantContext tenant, ICurrentUser currentUser, StockPushService stockPush, TimeProvider timeProvider)
{
    public const int MaxLines = 100;

    /// <summary>
    /// Saves a confirmed order with the next number (R-0001…) and takes its items from stock. Stock may go
    /// below zero: a shop order may have arrived a moment earlier, and the seller knows better.
    /// </summary>
    public async Task<ManualOrderResult> CreateAsync(CreateManualOrderCommand command, CancellationToken ct)
    {
        if (Validate(command) is { } error)
        {
            return ManualOrderResult.Failed(error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var itemIds = command.Lines.Select(l => l.ItemId).Distinct().ToList();
        var known = await db.Items.Where(i => itemIds.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct);
        for (var i = 0; i < command.Lines.Count; i++)
        {
            if (!known.Contains(command.Lines[i].ItemId))
            {
                return ManualOrderResult.Failed(new AppError("order.item_not_found", $"lines[{i}].itemId"));
            }
        }

        var now = timeProvider.GetUtcNow();
        var number = await db.NextManualOrderNumberAsync(ct);
        var (order, changes) = ExternalOrder.CreateManual(tenant.TenantId, number, command.Lines, command.Reference, command.Note, now);
        db.ExternalOrders.Add(order);

        await SaveAsync(order, changes, now, transaction, ct);
        return new ManualOrderResult(await orderList.GetAsync(order.Id, ct), []);
    }

    /// <summary>
    /// Cancels a manual order, returning its items to stock, or confirms a cancelled one again, taking
    /// them again. Shop orders are changed only by the shop (<c>order.not_manual</c>).
    /// </summary>
    public async Task<ManualOrderResult> SetStatusAsync(Guid orderId, ManualOrderStatusChange status, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Locks the order, so two changes of the same order run one after another.
        var order = await db.LockExternalOrderAsync(orderId, ct);
        if (order is null)
        {
            return ManualOrderResult.Missing;
        }

        if (order.Source != OrderSource.Manual)
        {
            return ManualOrderResult.Failed(new AppError("order.not_manual"));
        }

        var now = timeProvider.GetUtcNow();
        var changes = order.SetManualStatus(
            status == ManualOrderStatusChange.Cancelled ? ManualOrderStatus.Cancelled : ManualOrderStatus.Confirmed, now);

        await SaveAsync(order, changes, now, transaction, ct);
        return new ManualOrderResult(await orderList.GetAsync(order.Id, ct), []);
    }

    private async Task SaveAsync(
        ExternalOrder order, IReadOnlyList<OrderStockChange> changes, DateTimeOffset now,
        IDbContextTransaction transaction, CancellationToken ct)
    {
        var itemIds = await OrderMovements.RecordAsync(db, changes, now, StockMovementSource.Manual, currentUser.UserId, order.ExternalId, ct);
        var shops = await stockPush.MarkChangedAsync(itemIds, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        stockPush.Schedule(shops);
    }

    private static AppError? Validate(CreateManualOrderCommand command)
    {
        if (command.Lines.Count == 0) return new AppError("validation.required", "lines");
        if (command.Lines.Count > MaxLines)
        {
            return new AppError("validation.max_items", "lines", new Dictionary<string, object> { ["max"] = MaxLines });
        }

        for (var i = 0; i < command.Lines.Count; i++)
        {
            var quantity = command.Lines[i].Quantity;
            if (quantity <= 0) return new AppError("validation.positive", $"lines[{i}].quantity");
            if (!Item.HasAtMostDecimals(quantity, Item.QuantityDecimals))
            {
                return new AppError("validation.too_many_decimals", $"lines[{i}].quantity",
                    new Dictionary<string, object> { ["max"] = Item.QuantityDecimals });
            }
        }

        return null;
    }
}
