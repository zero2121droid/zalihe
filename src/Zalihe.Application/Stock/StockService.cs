using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Stock;

/// <summary>What the user records by hand. <see cref="Count"/> is an adjustment to a counted quantity.</summary>
public enum ManualMovementKind
{
    Receipt,
    Sale,
    Return,
    Count,
}

/// <param name="Quantity">Received, sold or returned amount (positive); for <see cref="ManualMovementKind.Count"/> the counted stock.</param>
public record RecordMovementCommand(ManualMovementKind Kind, decimal Quantity, string? Note);

public record StockMovementDto(
    Guid Id,
    StockMovementType Type,
    decimal Quantity,
    DateTimeOffset OccurredAt,
    StockMovementSource Source,
    string? ExternalRef,
    string? Note,
    string? UserName);

public record RecordMovementResult(StockMovementDto? Movement, decimal? Stock, IReadOnlyList<AppError> Errors, bool NotFound = false)
{
    public bool Succeeded => Movement is not null;

    public static RecordMovementResult Missing { get; } = new(null, null, [], NotFound: true);

    public static RecordMovementResult Failed(AppError error) => new(null, null, [error]);
}

public record StockSummaryDto(int BelowMinimum, int OutOfStock);

/// <summary>
/// Records stock movements and reads stock history. Every movement and the stock it changes
/// are saved in one transaction while the item's stock row is locked, so concurrent movements
/// for the same item are applied one after another and none is lost.
/// </summary>
public class StockService(IAppDbContext db, ICurrentUser currentUser, IUserDirectory users, TimeProvider timeProvider)
{
    public async Task<RecordMovementResult> RecordAsync(Guid itemId, RecordMovementCommand command, CancellationToken ct)
    {
        if (Validate(command) is { } error)
        {
            return RecordMovementResult.Failed(error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var item = await db.Items.SingleOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
        {
            return RecordMovementResult.Missing;
        }

        // Waits while another movement for this item is being saved, then reads the latest stock.
        var level = await db.LockStockLevelAsync(itemId, ct)
            ?? throw new InvalidOperationException($"Item {itemId} has no stock level.");

        var movement = Create(item, level.Quantity, command);
        if (movement is null)
        {
            return RecordMovementResult.Failed(new AppError("stock.no_change", "quantity"));
        }

        db.StockMovements.Add(movement);
        level.Apply(movement);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var userName = await UserNameAsync(movement.UserId, ct);
        return new RecordMovementResult(ToDto(movement, userName), level.Quantity, []);
    }

    /// <summary>How many active items need attention (see <see cref="StockStatusRules"/>).</summary>
    public async Task<StockSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var levels =
            from item in db.Items
            where item.IsActive
            join level in db.StockLevels on item.Id equals level.ItemId
            select new { level.Quantity, item.MinStock };

        var outOfStock = await levels.CountAsync(l => l.Quantity <= 0, ct);
        var belowMinimum = await levels.CountAsync(l => l.Quantity > 0 && l.Quantity <= l.MinStock, ct);
        return new StockSummaryDto(belowMinimum, outOfStock);
    }

    private static AppError? Validate(RecordMovementCommand command)
    {
        if (!Item.HasAtMostDecimals(command.Quantity, Item.QuantityDecimals))
        {
            return new AppError("validation.too_many_decimals", "quantity",
                new Dictionary<string, object> { ["max"] = Item.QuantityDecimals });
        }

        if (command.Kind == ManualMovementKind.Count)
        {
            if (command.Quantity < 0) return new AppError("validation.not_negative", "quantity");
            // A correction must always say why (count, write-off, damage).
            if (string.IsNullOrWhiteSpace(command.Note)) return new AppError("validation.required", "note");
        }
        else if (command.Quantity <= 0)
        {
            return new AppError("validation.positive", "quantity");
        }

        return null;
    }

    private StockMovement? Create(Item item, decimal currentStock, RecordMovementCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;
        const StockMovementSource manual = StockMovementSource.Manual;

        return command.Kind switch
        {
            ManualMovementKind.Receipt => StockMovement.Receipt(item, command.Quantity, now, manual, userId, command.Note),
            ManualMovementKind.Sale => StockMovement.Sale(item, command.Quantity, now, manual, userId, command.Note),
            ManualMovementKind.Return => StockMovement.Return(item, command.Quantity, now, manual, userId, command.Note),
            ManualMovementKind.Count => StockMovement.AdjustmentToCount(item, command.Quantity, currentStock, now, userId, command.Note),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
    }

    private async Task<string?> UserNameAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is not { } id) return null;
        var names = await users.GetNamesAsync([id], ct);
        return names.GetValueOrDefault(id);
    }

    internal static StockMovementDto ToDto(StockMovement m, string? userName) =>
        new(m.Id, m.Type, m.Quantity, m.OccurredAt, m.Source, m.ExternalRef, m.Note, userName);
}
