using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Application.Receipts;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Receipts;

/// <summary>
/// Goods received on a supplier's document, all lines at once (SPEC 11.7). There is no endpoint to
/// change or delete a receipt: a mistake is fixed with a correction, as with every movement.
/// </summary>
[ApiController]
[Authorize]
[Route("api/receipts")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ReceiptsController(ReceiptService receipts) : ControllerBase
{
    /// <summary>Posted receipts, newest first; searchable by supplier or document number.</summary>
    [HttpGet(Name = "ListReceipts")]
    [ProducesResponseType<PagedResult<ReceiptDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<ReceiptDto>> List(
        [FromQuery] string? search,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        receipts.ListAsync(new ReceiptListQuery(search, page, pageSize), ct);

    [HttpGet("{id:guid}", Name = "GetReceipt")]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await receipts.GetAsync(id, ct) is { } receipt ? Ok(receipt) : NotFound();

    /// <summary>
    /// Takes every line into stock at once and optionally makes the document's prices the items' purchase
    /// prices. The same supplier's document with the same number gives 409 <c>receipt.duplicate_document</c>
    /// (with <c>postedAt</c>), unless <c>allowDuplicate</c> is set.
    /// </summary>
    [HttpPost(Name = "CreateReceipt")]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(CreateReceiptRequest request, CancellationToken ct)
    {
        var lines = request.Lines!
            .Select(l => new ReceiptLineCommand(l.ItemId!.Value, l.Quantity!.Value, l.UnitPrice, l.UpdatePurchasePrice))
            .ToList();
        var result = await receipts.CreateAsync(
            new CreateReceiptCommand(request.SupplierName!, request.DocumentNumber, request.DocumentDate!.Value, lines, request.AllowDuplicate), ct);

        if (result.Duplicate)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status409Conflict, ReceiptService.DuplicateDocument, result.Errors);
        }

        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
        }

        return CreatedAtRoute("GetReceipt", new { id = result.Receipt!.Id }, result.Receipt);
    }
}
