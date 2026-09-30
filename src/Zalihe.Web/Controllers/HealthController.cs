using Microsoft.AspNetCore.Mvc;

namespace Zalihe.Web.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => new HealthResponse("ok");
}

public record HealthResponse(string Status);
