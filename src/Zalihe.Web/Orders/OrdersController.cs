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
public class OrdersController(OrderListService orderList) : ControllerBase
{
    /// <summary>
    /// Orders from the company's shops, newest first, with their products and the stock each took.
    /// Searchable by order number or product name; <paramref name="status"/> keeps one shop status,
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
}
