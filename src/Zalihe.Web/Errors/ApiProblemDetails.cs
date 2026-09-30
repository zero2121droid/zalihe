using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;

namespace Zalihe.Web.Errors;

/// <summary>
/// The single error format of the API: standard ProblemDetails plus an error code
/// and optional per-field errors. Clients translate codes, the API never sends messages.
/// </summary>
public class ApiProblemDetails : ProblemDetails
{
    public required string Code { get; init; }
    public IReadOnlyList<AppError> Errors { get; init; } = [];
}
