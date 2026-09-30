namespace Zalihe.Application.Common;

/// <summary>
/// An error returned to clients as a code (e.g. "auth.email_taken") that the frontend translates.
/// Never contains a translated message.
/// </summary>
/// <param name="Code">Error code, grouped by area (matches frontend "errors.*" keys).</param>
/// <param name="Field">Request field the error belongs to, in camelCase, if any.</param>
/// <param name="Params">Values used in the translated message, e.g. { "min": 8 }.</param>
public record AppError(string Code, string? Field = null, IReadOnlyDictionary<string, object>? Params = null);
