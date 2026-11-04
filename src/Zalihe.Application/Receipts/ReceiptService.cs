using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;
using Zalihe.Domain.Receipts;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Receipts;

/// <param name="UnitPrice">The purchase price per unit on the document; only for users who see purchase prices.</param>
/// <param name="UpdatePurchasePrice">Make <paramref name="UnitPrice"/> the item's purchase price (SPEC 11.7).</param>
public record ReceiptLineCommand(Guid ItemId, decimal Quantity, decimal? UnitPrice, bool UpdatePurchasePrice);

/// <param name="AllowDuplicate">Post even when the same supplier's document with this number is already posted.</param>
public record CreateReceiptCommand(
    string SupplierName,
    string? DocumentNumber,
    DateOnly DocumentDate,
    IReadOnlyList<ReceiptLineCommand> Lines,
    bool AllowDuplicate);

public record ReceiptItemDto(Guid Id, string Name, string Sku, Unit Unit);

/// <param name="UnitPrice">Null without a price, and for a user who may not see purchase prices.</param>
public record ReceiptLineDto(Guid Id, ReceiptItemDto Item, decimal Quantity, decimal? UnitPrice);

/// <param name="TotalValue">Quantity × price of the lines that have a price; null when none has one, or for a user who may not see purchase prices.</param>
/// <param name="CreatedByName">Who entered it; null when unknown.</param>
public record ReceiptDto(
    Guid Id,
    string SupplierName,
    string? DocumentNumber,
    DateOnly DocumentDate,
    DateTimeOffset PostedAt,
    string? CreatedByName,
    int LineCount,
    decimal? TotalValue,
    IReadOnlyList<ReceiptLineDto> Lines);

/// <param name="Search">Part of the supplier's name or the document number.</param>
public record ReceiptListQuery(string? Search = null, int Page = 1, int PageSize = Paging.DefaultPageSize);

public record ReceiptResult(ReceiptDto? Receipt, IReadOnlyList<AppError> Errors, bool Duplicate = false)
{
    public bool Succeeded => Receipt is not null;

    public static ReceiptResult Failed(AppError error) => new(null, [error]);
}

/// <summary>
/// Goods received on one document, all lines at once (SPEC 11.7, phase 0). Posting records a receipt
/// movement per line in one transaction, and the receipt never changes afterwards: a mistake is fixed
/// with a correction, as with every movement.
/// </summary>
public class ReceiptService(
    IAppDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IUserDirectory users,
    PurchasePriceAccess prices,
    StockPushService stockPush,
    TimeProvider timeProvider)
{
    public const int MaxLines = 200;
    public const string DuplicateDocument = "receipt.duplicate_document";

    public async Task<ReceiptResult> CreateAsync(CreateReceiptCommand command, CancellationToken ct)
    {
        if (await ValidateAsync(command, ct) is { } error)
        {
            return ReceiptResult.Failed(error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var itemIds = command.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        for (var i = 0; i < command.Lines.Count; i++)
        {
            if (!items.ContainsKey(command.Lines[i].ItemId))
            {
                return ReceiptResult.Failed(new AppError("receipt.item_not_found", $"lines[{i}].itemId"));
            }
        }

        // The same document twice is usually a mistake, but not always (e.g. a second delivery on one invoice).
        if (!command.AllowDuplicate && Receipt.KeyFor(command.SupplierName, command.DocumentNumber) is { } key)
        {
            var earlier = await db.Receipts
                .Where(r => r.DocumentKey == key && r.Status == ReceiptStatus.Posted)
                .OrderByDescending(r => r.PostedAt)
                .Select(r => r.PostedAt)
                .FirstOrDefaultAsync(ct);
            if (earlier is { } postedAt)
            {
                return new ReceiptResult(null,
                    [new AppError(DuplicateDocument, "documentNumber", new Dictionary<string, object> { ["postedAt"] = postedAt })],
                    Duplicate: true);
            }
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;
        var receipt = new Receipt(tenant.TenantId, command.SupplierName, command.DocumentNumber, command.DocumentDate, userId, now);
        foreach (var line in command.Lines)
        {
            receipt.AddLine(items[line.ItemId], line.Quantity, line.UnitPrice);
        }

        receipt.Post(now);
        db.Receipts.Add(receipt);

        // Items are always locked in the same order, so two receipts sharing items can't deadlock.
        foreach (var line in receipt.Lines.OrderBy(l => l.ItemId))
        {
            var level = await db.LockStockLevelAsync(line.ItemId, ct)
                ?? throw new InvalidOperationException($"Item {line.ItemId} has no stock level.");
            var movement = StockMovement.Receipt(
                items[line.ItemId], line.Quantity, now, StockMovementSource.Manual, userId,
                note: receipt.SupplierName, externalRef: receipt.DocumentNumber);
            db.StockMovements.Add(movement);
            level.Apply(movement);
        }

        // The user chose which prices become the items' purchase prices; the last line of an item wins.
        foreach (var line in command.Lines.Where(l => l.UpdatePurchasePrice && l.UnitPrice is not null).GroupBy(l => l.ItemId))
        {
            var item = items[line.Key];
            var changes = item.ChangePurchasePrice(line.Last().UnitPrice);
            if (ItemChange.Updated(item, changes, userId, now) is { } change)
            {
                db.ItemChanges.Add(change);
            }
        }

        var shops = await stockPush.MarkChangedAsync(itemIds, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        stockPush.Schedule(shops);

        return new ReceiptResult(await GetAsync(receipt.Id, ct), []);
    }

    public async Task<PagedResult<ReceiptDto>> ListAsync(ReceiptListQuery query, CancellationToken ct)
    {
        var receipts = db.Receipts.AsNoTracking().Where(r => r.Status == ReceiptStatus.Posted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            receipts = receipts.Where(r =>
                r.SupplierName.ToLower().Contains(term)
                || (r.DocumentNumber != null && r.DocumentNumber.ToLower().Contains(term)));
        }

        var totalCount = await receipts.CountAsync(ct);
        var page = await receipts
            .OrderByDescending(r => r.PostedAt)
            .ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(r => r.Lines)
            .ToListAsync(ct);

        return new PagedResult<ReceiptDto>(await ToDtosAsync(page, ct), totalCount, query.Page, query.PageSize);
    }

    /// <summary>One posted receipt with its lines; null when the company has no such receipt.</summary>
    public async Task<ReceiptDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var receipt = await db.Receipts.AsNoTracking().Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.Id == id && r.Status == ReceiptStatus.Posted, ct);
        return receipt is null ? null : (await ToDtosAsync([receipt], ct))[0];
    }

    private async Task<IReadOnlyList<ReceiptDto>> ToDtosAsync(IReadOnlyList<Receipt> receipts, CancellationToken ct)
    {
        var canSeePrices = await prices.CanSeeAsync(ct);
        var itemIds = receipts.SelectMany(r => r.Lines).Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => new ReceiptItemDto(i.Id, i.Name, i.Sku, i.Unit), ct);
        var names = await users.GetNamesAsync(receipts.Select(r => r.CreatedBy).OfType<Guid>().Distinct().ToList(), ct);

        return receipts.Select(r =>
        {
            var priced = r.Lines.Where(l => l.UnitPrice is not null).ToList();
            return new ReceiptDto(
                r.Id,
                r.SupplierName,
                r.DocumentNumber,
                r.DocumentDate,
                r.PostedAt!.Value,
                r.CreatedBy is { } by ? names.GetValueOrDefault(by) : null,
                r.Lines.Count,
                canSeePrices && priced.Count > 0 ? priced.Sum(l => l.Quantity * l.UnitPrice!.Value) : null,
                r.Lines
                    .OrderBy(l => l.Position)
                    .Select(l => new ReceiptLineDto(l.Id, items[l.ItemId], l.Quantity, canSeePrices ? l.UnitPrice : null))
                    .ToList());
        }).ToList();
    }

    private async Task<AppError?> ValidateAsync(CreateReceiptCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.SupplierName)) return new AppError("validation.required", "supplierName");
        if (command.Lines.Count == 0) return new AppError("validation.required", "lines");
        if (command.Lines.Count > MaxLines)
        {
            return new AppError("validation.max_items", "lines", new Dictionary<string, object> { ["max"] = MaxLines });
        }

        var canSeePrices = await prices.CanSeeAsync(ct);
        for (var i = 0; i < command.Lines.Count; i++)
        {
            var line = command.Lines[i];
            if (line.Quantity <= 0) return new AppError("validation.positive", $"lines[{i}].quantity");
            if (!Item.HasAtMostDecimals(line.Quantity, Item.QuantityDecimals))
            {
                return new AppError("validation.too_many_decimals", $"lines[{i}].quantity",
                    new Dictionary<string, object> { ["max"] = Item.QuantityDecimals });
            }

            // An employee without the right to see prices may receive goods, but not send prices (SPEC 11.2).
            if ((line.UnitPrice is not null || line.UpdatePurchasePrice) && !canSeePrices)
            {
                return new AppError(PurchasePriceAccess.NotAllowed, $"lines[{i}].unitPrice");
            }

            if (line.UnitPrice is { } price)
            {
                if (price < 0) return new AppError("validation.not_negative", $"lines[{i}].unitPrice");
                if (!Item.HasAtMostDecimals(price, Item.MoneyDecimals))
                {
                    return new AppError("validation.too_many_decimals", $"lines[{i}].unitPrice",
                        new Dictionary<string, object> { ["max"] = Item.MoneyDecimals });
                }
            }
        }

        return null;
    }
}
