using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Items;

public record ItemDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    Unit Unit,
    string? Category,
    string? GroupName,
    decimal? PurchasePrice,
    decimal? SalePrice,
    decimal MinStock,
    bool IsActive);

public record ItemListQuery(
    string? Search,
    string? Category,
    bool IncludeInactive = false,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);

/// <summary>Item data for both creating and updating an item.</summary>
public record SaveItemCommand(
    string Name,
    string Sku,
    string? Barcode,
    Unit Unit,
    string? Category,
    string? GroupName,
    decimal? PurchasePrice,
    decimal? SalePrice,
    decimal MinStock);

public record SaveItemResult(ItemDto? Item, IReadOnlyList<AppError> Errors, bool NotFound = false)
{
    public bool Succeeded => Item is not null;

    public static SaveItemResult Missing { get; } = new(null, [], NotFound: true);
}

/// <summary>
/// Items of the current company. Tenant filtering is done by the global query filter,
/// so no query here mentions TenantId.
/// </summary>
public class ItemService(IAppDbContext db, ITenantContext tenant, TimeProvider timeProvider)
{
    public async Task<PagedResult<ItemDto>> ListAsync(ItemListQuery query, CancellationToken ct)
    {
        var items = db.Items.AsNoTracking();

        if (!query.IncludeInactive)
        {
            items = items.Where(i => i.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            items = items.Where(i =>
                i.Name.ToLower().Contains(term)
                || i.Sku.ToLower().Contains(term)
                || (i.Barcode != null && i.Barcode.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            items = items.Where(i => i.Category == category);
        }

        var totalCount = await items.CountAsync(ct);
        var page = await items
            .OrderBy(i => i.Name)
            .ThenBy(i => i.Sku)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync(ct);

        return new PagedResult<ItemDto>(page, totalCount, query.Page, query.PageSize);
    }

    public Task<ItemDto?> GetAsync(Guid id, CancellationToken ct) =>
        db.Items.AsNoTracking().Where(i => i.Id == id).Select(ToDto).SingleOrDefaultAsync(ct);

    /// <summary>Distinct categories in use, for suggestions in the item form.</summary>
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct) =>
        await db.Items
            .Where(i => i.Category != null)
            .Select(i => i.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

    public async Task<SaveItemResult> CreateAsync(SaveItemCommand command, CancellationToken ct)
    {
        var errors = await ValidateAsync(command, itemId: null, ct);
        if (errors.Count > 0)
        {
            return new SaveItemResult(null, errors);
        }

        var item = new Item(
            tenant.TenantId,
            command.Name,
            command.Sku,
            command.Unit,
            command.MinStock,
            timeProvider.GetUtcNow(),
            command.Barcode,
            command.Category,
            command.GroupName,
            command.PurchasePrice,
            command.SalePrice);

        db.Items.Add(item);
        await db.SaveChangesAsync(ct);

        return new SaveItemResult(MapToDto(item), []);
    }

    /// <summary>Changes an item of the current company; another company's item is "not found".</summary>
    public async Task<SaveItemResult> UpdateAsync(Guid id, SaveItemCommand command, CancellationToken ct)
    {
        var item = await db.Items.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (item is null)
        {
            return SaveItemResult.Missing;
        }

        var errors = await ValidateAsync(command, id, ct);
        if (errors.Count > 0)
        {
            return new SaveItemResult(null, errors);
        }

        item.Update(
            command.Name,
            command.Sku,
            command.Unit,
            command.MinStock,
            command.Barcode,
            command.Category,
            command.GroupName,
            command.PurchasePrice,
            command.SalePrice);
        await db.SaveChangesAsync(ct);

        return new SaveItemResult(MapToDto(item), []);
    }

    /// <summary>Deactivates or activates an item. Returns false when the item doesn't exist.</summary>
    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var item = await db.Items.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (item is null)
        {
            return false;
        }

        if (isActive) item.Activate();
        else item.Deactivate();
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Rules that need the database or parameters in the message. Required fields, lengths and
    /// non-negative values are validated on the request already.
    /// </summary>
    private async Task<List<AppError>> ValidateAsync(SaveItemCommand command, Guid? itemId, CancellationToken ct)
    {
        var errors = new List<AppError>();
        CheckDecimals(errors, command.MinStock, Item.QuantityDecimals, "minStock");
        CheckDecimals(errors, command.PurchasePrice, Item.MoneyDecimals, "purchasePrice");
        CheckDecimals(errors, command.SalePrice, Item.MoneyDecimals, "salePrice");

        var sku = command.Sku.Trim();
        if (await db.Items.AnyAsync(i => i.Sku == sku && i.Id != itemId, ct))
        {
            errors.Add(new AppError("item.sku_duplicate", "sku"));
        }

        return errors;
    }

    private static void CheckDecimals(List<AppError> errors, decimal? value, int decimals, string field)
    {
        if (value is { } v && !Item.HasAtMostDecimals(v, decimals))
        {
            errors.Add(new AppError("validation.too_many_decimals", field,
                new Dictionary<string, object> { ["max"] = decimals }));
        }
    }

    private static readonly System.Linq.Expressions.Expression<Func<Item, ItemDto>> ToDto = i => new ItemDto(
        i.Id, i.Name, i.Sku, i.Barcode, i.Unit, i.Category, i.GroupName,
        i.PurchasePrice, i.SalePrice, i.MinStock, i.IsActive);

    private static readonly Func<Item, ItemDto> MapToDto = ToDto.Compile();
}
