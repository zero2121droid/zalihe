using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Domain.Feedback;
using Zalihe.Infrastructure.Platform;
using Zalihe.Web.Auth;
using Zalihe.Web.Errors;
using Zalihe.Web.Feedback;

namespace Zalihe.Web.Platform;

/// <summary>How the companies use Lagerko (SPEC 11.8), only for the platform's owner.</summary>
[ApiController]
[Authorize(Policy = Policies.PlatformOwner)]
[Route("api/platform")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class PlatformController(PlatformStatsService stats, PlatformFeedbackService feedback) : ControllerBase
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

    /// <summary>Counts of the users' messages (SPEC 11.10), and how clear the CSV import was.</summary>
    [HttpGet("feedback/summary", Name = "GetPlatformFeedbackSummary")]
    [ProducesResponseType<PlatformFeedbackSummaryDto>(StatusCodes.Status200OK)]
    public Task<PlatformFeedbackSummaryDto> FeedbackSummary(CancellationToken ct) => feedback.GetSummaryAsync(ct);

    /// <summary>The users' messages from all companies, newest first; a plain "yes" to a prompt is only counted.</summary>
    [HttpGet("feedback", Name = "ListPlatformFeedback")]
    [ProducesResponseType<PagedResult<PlatformFeedbackDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<PlatformFeedbackDto>> Feedback(
        [FromQuery] FeedbackStatus? status,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        feedback.ListAsync(new PlatformFeedbackListQuery(status, page, pageSize), ct);

    /// <summary>Marks a message as new, read or done.</summary>
    [HttpPut("feedback/{id:guid}/status", Name = "SetPlatformFeedbackStatus")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SetFeedbackStatus(Guid id, SetFeedbackStatusRequest request, CancellationToken ct) =>
        await feedback.SetStatusAsync(id, request.Status!.Value, ct) ? NoContent() : NotFound();

    /// <summary>The picture of the screen attached to a message, until the message is marked done.</summary>
    [HttpGet("feedback/{id:guid}/screenshot", Name = "GetPlatformFeedbackScreenshot")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> FeedbackScreenshot(Guid id, CancellationToken ct)
    {
        if (await feedback.GetScreenshotAsync(id, ct) is not { } screenshot)
        {
            return NotFound();
        }

        // Only for the owner, never cached, never sniffed or run as anything but a picture.
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return new FileContentResult(screenshot.Data, screenshot.ContentType);
    }
}
