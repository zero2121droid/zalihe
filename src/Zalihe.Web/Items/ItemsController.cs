using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Application.Items;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Items;

[ApiController]
[Authorize]
[Route("api/items")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ItemsController(ItemService itemService, ItemHistoryService historyService, ItemImageService imageService) : ControllerBase
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

    /// <summary>
    /// Everything that happened to the item, newest first: stock movements and changes of the
    /// item itself (created, edited, deactivated, activated), with who did it.
    /// </summary>
    [HttpGet("{id:guid}/history", Name = "GetItemHistory")]
    [ProducesResponseType<PagedResult<ItemHistoryEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PagedResult<ItemHistoryEntryDto>>> History(
        Guid id,
        [FromQuery] ItemHistoryFilter filter = ItemHistoryFilter.All,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        await historyService.GetAsync(id, filter, page, pageSize, ct) is { } history ? history : NotFound();

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

    /// <summary>
    /// Sets the item's picture (JPEG, PNG or WebP, at most 2 MB), replacing the previous one. It is shown
    /// instead of the shop's product image. The web app shrinks pictures before sending them.
    /// </summary>
    [HttpPut("{id:guid}/image", Name = "SetItemImage")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [ProducesResponseType<ItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SetImage(Guid id, [Required(ErrorMessage = "validation.required")] IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        var result = await imageService.SetAsync(id, content, file.Length, ct);
        if (result.NotFound)
        {
            return NotFound();
        }

        return result.Succeeded ? Ok(result.Item) : ValidationFailed(result);
    }

    /// <summary>The picture the user added to the item. Its address changes with every new picture, so it is cached for good.</summary>
    [HttpGet("{id:guid}/image", Name = "GetItemImage")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken ct)
    {
        if (await imageService.GetAsync(id, ct) is not { } image)
        {
            return NotFound();
        }

        // Private: only the company's users may see it. Never sniffed or run as anything but a picture.
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return new FileContentResult(image.Data, image.ContentType) { LastModified = image.UpdatedAt };
    }

    /// <summary>Removes the picture the user added; the shop's product image shows again, if there is one.</summary>
    [HttpDelete("{id:guid}/image", Name = "RemoveItemImage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> RemoveImage(Guid id, CancellationToken ct) =>
        await imageService.RemoveAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// Room for a picture larger than allowed plus the form around it, so the service can answer with
    /// its own error code; anything larger is cut off by the server.
    /// </summary>
    private const long ImageRequestLimit = ItemImage.MaxBytes * 2L;

    private static SaveItemCommand ToCommand(ItemRequest request) => new(
        request.Name, request.Sku, request.Barcode, request.Unit!.Value, request.Category,
        request.GroupName, request.PurchasePrice, request.SalePrice, request.MinStock);

    private ObjectResult ValidationFailed(SaveItemResult result) =>
        ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
}
