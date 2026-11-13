using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Zalihe.Application.Common;
using Zalihe.Application.Feedback;
using Zalihe.Domain.Feedback;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Feedback;

/// <summary>Messages from users to the platform's owner (SPEC 11.10); any signed-in user may send them.</summary>
[ApiController]
[Authorize]
[Route("api/feedback")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class FeedbackController(FeedbackService feedback) : ControllerBase
{
    /// <summary>
    /// Sends a suggestion, a problem or something unneeded or confusing, with an optional picture of the
    /// screen. A form, so the picture goes in the same request: the message is never saved without it.
    /// </summary>
    [HttpPost(Name = "SendFeedback")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    /// <param name="kind">Suggestion, problem or something unneeded or confusing; not "prompt".</param>
    /// <param name="message">What the user wrote, at most 4000 characters.</param>
    /// <param name="page">The app's path the user was on, e.g. "/items".</param>
    /// <param name="mayContact">The user agrees to be emailed about it.</param>
    /// <param name="screenshot">A picture of the screen (JPEG, PNG or WebP, at most 2 MB); optional.</param>
    // Separate form fields, not a record, so their names stay camelCase in OpenAPI like the rest of the API.
    public async Task<IActionResult> Send(
        [FromForm, Required(ErrorMessage = "validation.required")]
        [AllowedValues(FeedbackKind.Suggestion, FeedbackKind.Problem, FeedbackKind.Confusing, ErrorMessage = "validation.invalid")]
        FeedbackKind? kind,
        [FromForm, Required(ErrorMessage = "validation.required")]
        [MaxLength(FeedbackMessage.MessageMaxLength, ErrorMessage = "validation.max_length")]
        string? message,
        [FromForm, MaxLength(FeedbackMessage.PageMaxLength, ErrorMessage = "validation.max_length")]
        string? page,
        [FromForm] bool mayContact,
        IFormFile? screenshot,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Invalid(new AppError("validation.required", "message"));
        }

        byte[]? picture = null;
        if (screenshot is { } file)
        {
            if (FeedbackService.CheckScreenshotLength(file.Length) is { } tooLarge) return Invalid(tooLarge);
            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, ct);
            picture = buffer.ToArray();
        }

        var error = await feedback.SendAsync(
            new SendFeedbackCommand(kind!.Value, message, page, UserAgent(), mayContact, picture), ct);
        return error switch
        {
            null => NoContent(),
            { Code: FeedbackService.TooManyCode } => TooMany(error.Code),
            _ => Invalid(error),
        };
    }

    /// <summary>Answers a question the app asks once per user; answering again changes nothing.</summary>
    [HttpPost("prompts/{prompt}", Name = "AnswerFeedbackPrompt")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Answer(FeedbackPrompt prompt, AnswerPromptRequest request, CancellationToken ct)
    {
        var error = await feedback.AnswerAsync(
            new AnswerPromptCommand(prompt, request.Positive!.Value, request.Message, request.Page, UserAgent()), ct);
        return error is null ? NoContent() : TooMany(error.Code);
    }

    /// <summary>The questions the signed-in user has already answered.</summary>
    [HttpGet("prompts", Name = "ListAnsweredFeedbackPrompts")]
    [ProducesResponseType<IReadOnlyList<FeedbackPrompt>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<FeedbackPrompt>> AnsweredPrompts(CancellationToken ct) => feedback.AnsweredPromptsAsync(ct);

    private string? UserAgent() => Request.Headers[HeaderNames.UserAgent].ToString();

    /// <summary>
    /// Room for a picture larger than allowed plus the form around it, so the service answers with its own
    /// error code; anything larger is cut off by the server.
    /// </summary>
    private const long RequestLimit = FeedbackScreenshot.MaxBytes * 2L;

    private ObjectResult Invalid(AppError error) =>
        ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, [error]);

    private ObjectResult TooMany(string code) =>
        ApiProblems.Result(HttpContext, StatusCodes.Status429TooManyRequests, code);
}
