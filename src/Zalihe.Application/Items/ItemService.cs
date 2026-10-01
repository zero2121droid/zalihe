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

public record ItemListQuery(string? Search, string? Category, int Page = 1, int PageSize = Paging.DefaultPageSize);

public record CreateItemCommand(
    string Name,
    string Sku,
    string? Barcode,
    Unit Unit,
    string? Category,
    string? GroupName,
    decimal? PurchasePrice,
    decimal? SalePrice,
    decimal MinStock);

public record CreateItemResult(ItemDto? Item, IReadOnlyList<AppError> Errors)
{
    public bool Succeeded => Item is not null;
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

    public async Task<CreateItemResult> CreateAsync(CreateItemCommand command, CancellationToken ct)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
        {
            return new CreateItemResult(null, errors);
        }

        var sku = command.Sku.Trim();
        if (await db.Items.AnyAsync(i => i.Sku == sku, ct))
        {
            return new CreateItemResult(null, [new AppError("item.sku_duplicate", "sku")]);
        }

        var item = new Item(
            tenant.TenantId,
            command.Name,
            sku,
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

        return new CreateItemResult(MapToDto(item), []);
    }

    /// <summary>
    /// Rules that need parameters in the message. Required fields, lengths and
    /// non-negative values are validated on the request already.
    /// </summary>
    private static List<AppError> Validate(CreateItemCommand command)
    {
        var errors = new List<AppError>();
        CheckDecimals(errors, command.MinStock, Item.QuantityDecimals, "minStock");
        CheckDecimals(errors, command.PurchasePrice, Item.MoneyDecimals, "purchasePrice");
        CheckDecimals(errors, command.SalePrice, Item.MoneyDecimals, "salePrice");
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
