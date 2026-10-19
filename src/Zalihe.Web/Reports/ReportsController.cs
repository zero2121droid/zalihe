using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Application.Reports;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Reports;

/// <summary>Reports over the company's active items. Searchable lists match the item's name or SKU.</summary>
[ApiController]
[Authorize]
[Route("api/reports")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ReportsController(ReportService reportService) : ControllerBase
{
    /// <summary>Stock value, items missing from it for lack of a purchase price, and the stock that doesn't sell.</summary>
    [HttpGet("summary", Name = "GetReportSummary")]
    [ProducesResponseType<ReportSummaryDto>(StatusCodes.Status200OK)]
    public Task<ReportSummaryDto> Summary([FromQuery] string? category, CancellationToken ct) =>
        reportService.GetSummaryAsync(category, ct);

    /// <summary>Items that sold the most in the last 30 days (sales less returns), most first.</summary>
    [HttpGet("top-sellers", Name = "ListTopSellers")]
    [ProducesResponseType<PagedResult<TopSellerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<TopSellerDto>> TopSellers(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        reportService.ListTopSellersAsync(new ReportQuery(search, category, page, pageSize), ct);

    /// <summary>
    /// Items in stock with no sale in the last 60 days (items added in that time are left out),
    /// the most money tied up first.
    /// </summary>
    [HttpGet("slow-movers", Name = "ListSlowMovers")]
    [ProducesResponseType<PagedResult<SlowMoverDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<SlowMoverDto>> SlowMovers(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        reportService.ListSlowMoversAsync(new ReportQuery(search, category, page, pageSize), ct);

    /// <summary>Stock value at purchase price per category, the most valuable first.</summary>
    [HttpGet("stock-value", Name = "ListCategoryValues")]
    [ProducesResponseType<PagedResult<CategoryValueDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<CategoryValueDto>> StockValue(
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        reportService.ListCategoryValuesAsync(page, pageSize, ct);
}
