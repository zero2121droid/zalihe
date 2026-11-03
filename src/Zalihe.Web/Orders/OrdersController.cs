using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Application.Orders;
using Zalihe.Domain.Orders;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Orders;

[ApiController]
[Authorize]
[Route("api/orders")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class OrdersController(OrderListService orderList, ManualOrderService manualOrders) : ControllerBase
{
    /// <summary>
    /// The company's orders, from its shops and manual ones, newest first, with their products and the
    /// stock each took. Searchable by order number, reference or product name; <paramref name="status"/> keeps one shop status,
    /// <paramref name="unlinkedOnly"/> only orders with products not linked to an item.
    /// </summary>
    [HttpGet(Name = "ListOrders")]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<OrderDto>> List(
        [FromQuery] string? search,
        [FromQuery, MaxLength(ExternalOrder.StatusMaxLength, ErrorMessage = "validation.max_length")] string? status,
        [FromQuery] bool unlinkedOnly = false,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        orderList.ListAsync(new OrderListQuery(search, status, unlinkedOnly, page, pageSize), ct);

    /// <summary>
    /// Records a manual order (a sale outside the shop: Instagram, phone, store) and takes its items
    /// from stock. It gets the next number (R-0001…) and is confirmed; stock may go below zero.
    /// </summary>
    [HttpPost(Name = "CreateOrder")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var lines = request.Lines!.Select(l => new ManualOrderLine(l.ItemId!.Value, l.Quantity!.Value)).ToList();
        var result = await manualOrders.CreateAsync(new CreateManualOrderCommand(lines, request.Reference, request.Note), ct);

        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
        }

        return StatusCode(StatusCodes.Status201Created, result.Order);
    }

    /// <summary>
    /// Cancels a manual order, returning its items to stock, or confirms a cancelled one again. A shop
    /// order is changed only by its shop: 409 with <c>order.not_manual</c>.
    /// </summary>
    [HttpPut("{id:guid}/status", Name = "SetOrderStatus")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> SetStatus(Guid id, SetOrderStatusRequest request, CancellationToken ct)
    {
        var result = await manualOrders.SetStatusAsync(id, request.Status!.Value, ct);

        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status409Conflict, result.Errors[0].Code);
        }

        return Ok(result.Order);
    }
}
