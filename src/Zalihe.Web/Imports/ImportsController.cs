using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Imports;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Imports;

/// <summary>
/// Item import from CSV in three calls with the same file: analyze, preview, import.
/// The file is sent each time, so the server keeps no state between steps.
/// </summary>
[ApiController]
[Authorize]
[Route("api/imports/items")]
[RequestSizeLimit(MaxFileBytes + 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 1024 * 1024)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ImportsController(ItemImportService importService) : ControllerBase
{
    private const int MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Columns of the file, a few sample rows and a suggested mapping.</summary>
    [HttpPost("analyze", Name = "AnalyzeItemImport")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ImportAnalysisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Analyze([FromForm] ImportFileForm form, CancellationToken ct)
    {
        if (await ReadAsync(form, ct) is not { } content) return FileTooLarge();
        return Respond(importService.Analyze(content));
    }

    /// <summary>What would be imported, and which rows won't be (errors, existing SKUs).</summary>
    [HttpPost("preview", Name = "PreviewItemImport")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ImportPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Preview([FromForm] ItemImportForm form, CancellationToken ct)
    {
        if (await ReadAsync(form, ct) is not { } content) return FileTooLarge();
        return Respond(await importService.PreviewAsync(content, form.ToMapping(), form.DefaultUnit, ct));
    }

    /// <summary>Imports the valid rows with their opening stock; other rows are skipped.</summary>
    [HttpPost(Name = "ImportItems")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Import([FromForm] ItemImportForm form, CancellationToken ct)
    {
        if (await ReadAsync(form, ct) is not { } content) return FileTooLarge();
        return Respond(await importService.ImportAsync(content, form.ToMapping(), form.DefaultUnit, ct));
    }

    private static async Task<byte[]?> ReadAsync(ImportFileForm form, CancellationToken ct)
    {
        if (form.File!.Length > MaxFileBytes) return null;
        using var stream = new MemoryStream();
        await form.File.CopyToAsync(stream, ct);
        return stream.ToArray();
    }

    private IActionResult FileTooLarge() =>
        ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed,
            [new("import.file_too_large", "file", new Dictionary<string, object> { ["maxMb"] = MaxFileBytes / 1024 / 1024 })]);

    private IActionResult Respond<T>(ImportFileResult<T> result) =>
        result.Error is { } error
            ? ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, [error])
            : Ok(result.Value);
}
