using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Application.Stock;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Stock;

/// <summary>
/// Stock movements. There is no endpoint to change or delete a movement: a mistake is fixed
/// with a new one (a correction).
/// </summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class StockController(StockService stockService) : ControllerBase
{
    /// <summary>Records a receipt, sale, return or a correction to a counted quantity.</summary>
    [HttpPost("api/items/{itemId:guid}/movements", Name = "RecordStockMovement")]
    [ProducesResponseType<RecordMovementResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Record(Guid itemId, RecordMovementRequest request, CancellationToken ct)
    {
        var result = await stockService.RecordAsync(
            itemId, new RecordMovementCommand(request.Kind!.Value, request.Quantity!.Value, request.Note), ct);

        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
        }

        return StatusCode(StatusCodes.Status201Created, new RecordMovementResponse(result.Movement!, result.Stock!.Value));
    }

    /// <summary>Movements of an item, newest first.</summary>
    [HttpGet("api/items/{itemId:guid}/movements", Name = "ListStockMovements")]
    [ProducesResponseType<PagedResult<StockMovementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> History(
        Guid itemId,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        await stockService.GetHistoryAsync(itemId, page, pageSize, ct) is { } history ? history : NotFound();

    /// <summary>Counts of active items below minimum and out of stock.</summary>
    [HttpGet("api/stock/summary", Name = "GetStockSummary")]
    [ProducesResponseType<StockSummaryDto>(StatusCodes.Status200OK)]
    public Task<StockSummaryDto> Summary(CancellationToken ct) => stockService.GetSummaryAsync(ct);
}
