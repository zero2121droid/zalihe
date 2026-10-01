using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Dashboard;

namespace Zalihe.Web.Dashboard;

[ApiController]
[Authorize]
[Route("api/dashboard")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class DashboardController(DashboardService dashboardService) : ControllerBase
{
    /// <summary>Home screen overview: stock value and counts, items to reorder, latest movements.</summary>
    [HttpGet(Name = "GetDashboard")]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    public Task<DashboardDto> Get(CancellationToken ct) => dashboardService.GetAsync(ct);
}
