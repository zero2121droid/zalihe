using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Infrastructure.Platform;
using Zalihe.Web.Auth;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Platform;

/// <summary>How the companies use Lagerko (SPEC 11.8), only for the platform's owner.</summary>
[ApiController]
[Authorize(Policy = Policies.PlatformOwner)]
[Route("api/platform")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class PlatformController(PlatformStatsService stats) : ControllerBase
{
    /// <summary>Totals over all companies except the owner's own.</summary>
    [HttpGet("summary", Name = "GetPlatformSummary")]
    [ProducesResponseType<PlatformSummaryDto>(StatusCodes.Status200OK)]
    public Task<PlatformSummaryDto> Summary(CancellationToken ct) => stats.GetSummaryAsync(ct);

    /// <summary>All companies, newest first; searchable by name.</summary>
    [HttpGet("companies", Name = "ListPlatformCompanies")]
    [ProducesResponseType<PagedResult<PlatformCompanyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<PlatformCompanyDto>> Companies(
        [FromQuery] string? search,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        stats.ListCompaniesAsync(new PlatformCompanyListQuery(search, page, pageSize), ct);
}
