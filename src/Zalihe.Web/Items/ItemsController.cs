using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Application.Items;
using Zalihe.Domain.Stock;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Items;

[ApiController]
[Authorize]
[Route("api/items")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ItemsController(ItemService itemService) : ControllerBase
{
    /// <summary>
    /// Items of the current company, paged, searchable by name, SKU or barcode.
    /// Deactivated items are left out unless <paramref name="includeInactive"/> is set;
    /// <paramref name="status"/> keeps only items in that stock status.
    /// </summary>
    [HttpGet(Name = "ListItems")]
    [ProducesResponseType<PagedResult<ItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<ItemDto>> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] bool includeInactive = false,
        [FromQuery] StockStatus? status = null,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        itemService.ListAsync(new ItemListQuery(search, category, includeInactive, status, page, pageSize), ct);

    [HttpGet("{id:guid}", Name = "GetItem")]
    [ProducesResponseType<ItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ItemDto>> Get(Guid id, CancellationToken ct) =>
        await itemService.GetAsync(id, ct) is { } item ? item : NotFound();

    /// <summary>Categories in use, for suggestions in the item form.</summary>
    [HttpGet("categories", Name = "ListItemCategories")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<string>> Categories(CancellationToken ct) => itemService.GetCategoriesAsync(ct);

    [HttpPost(Name = "CreateItem")]
    [ProducesResponseType<ItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Create(ItemRequest request, CancellationToken ct)
    {
        var result = await itemService.CreateAsync(ToCommand(request), ct);
        if (!result.Succeeded)
        {
            return ValidationFailed(result);
        }

        return CreatedAtRoute("GetItem", new { id = result.Item!.Id }, result.Item);
    }

    [HttpPut("{id:guid}", Name = "UpdateItem")]
    [ProducesResponseType<ItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Update(Guid id, ItemRequest request, CancellationToken ct)
    {
        var result = await itemService.UpdateAsync(id, ToCommand(request), ct);
        if (result.NotFound)
        {
            return NotFound();
        }

        return result.Succeeded ? Ok(result.Item) : ValidationFailed(result);
    }

    /// <summary>Hides the item from lists. Items are never deleted, so their history stays.</summary>
    [HttpPost("{id:guid}/deactivate", Name = "DeactivateItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        await itemService.SetActiveAsync(id, isActive: false, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/activate", Name = "ActivateItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) =>
        await itemService.SetActiveAsync(id, isActive: true, ct) ? NoContent() : NotFound();

    private static SaveItemCommand ToCommand(ItemRequest request) => new(
        request.Name, request.Sku, request.Barcode, request.Unit!.Value, request.Category,
        request.GroupName, request.PurchasePrice, request.SalePrice, request.MinStock);

    private ObjectResult ValidationFailed(SaveItemResult result) =>
        ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
}
