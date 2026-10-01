using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Application.Items;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Items;

[ApiController]
[Authorize]
[Route("api/items")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ItemsController(ItemService itemService) : ControllerBase
{
    /// <summary>Items of the current company, paged, searchable by name, SKU or barcode.</summary>
    [HttpGet(Name = "ListItems")]
    [ProducesResponseType<PagedResult<ItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<ItemDto>> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        itemService.ListAsync(new ItemListQuery(search, category, page, pageSize), ct);

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
    public async Task<IActionResult> Create(CreateItemRequest request, CancellationToken ct)
    {
        var result = await itemService.CreateAsync(
            new CreateItemCommand(
                request.Name, request.Sku, request.Barcode, request.Unit!.Value, request.Category,
                request.GroupName, request.PurchasePrice, request.SalePrice, request.MinStock),
            ct);

        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
        }

        return CreatedAtRoute("GetItem", new { id = result.Item!.Id }, result.Item);
    }
}
