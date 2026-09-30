using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Zalihe.Application.Common;

namespace Zalihe.Web.Errors;

public static class ApiProblems
{
    public const string ValidationFailed = "validation.failed";
    private const string InvalidValue = "validation.invalid";

    public static ObjectResult Result(HttpContext httpContext, int status, string code, IReadOnlyList<AppError>? errors = null)
    {
        var problem = new ApiProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Code = code,
            Errors = errors ?? [],
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    /// <summary>
    /// Turns model validation errors into codes. Validation attributes use codes as their
    /// ErrorMessage (e.g. "validation.required"); framework messages (such as JSON parse errors)
    /// become <see cref="InvalidValue"/>.
    /// </summary>
    public static IActionResult FromInvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
                new AppError(ToCode(error.ErrorMessage), ToFieldName(entry.Key))))
            .ToList();

        return Result(context.HttpContext, StatusCodes.Status400BadRequest, ValidationFailed, errors);
    }

    /// <summary>Adds a code to problems produced by the framework (401, 404, unhandled exceptions...).</summary>
    public static void AddDefaultCode(ProblemDetailsContext context)
    {
        if (context.ProblemDetails is ApiProblemDetails || context.ProblemDetails.Extensions.ContainsKey("code"))
        {
            return;
        }

        context.ProblemDetails.Extensions["code"] = context.ProblemDetails.Status switch
        {
            StatusCodes.Status401Unauthorized => "auth.unauthenticated",
            StatusCodes.Status403Forbidden => "auth.forbidden",
            StatusCodes.Status404NotFound => "common.not_found",
            >= 500 => "common.unexpected",
            _ => "common.request_failed",
        };
    }

    private static string ToCode(string message) =>
        message.Length > 0 && !message.Contains(' ') && message.Contains('.') ? message : InvalidValue;

    private static string? ToFieldName(string key)
    {
        key = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return key is "" or "$" ? null : char.ToLowerInvariant(key[0]) + key[1..];
    }
}
