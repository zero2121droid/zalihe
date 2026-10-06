using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Channels;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Channels;

/// <summary>Connected online shops. API keys are accepted here but never returned.</summary>
[ApiController]
[Authorize]
[Route("api/channels")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ChannelsController(ChannelService channelService, ProductImportService productImport) : ControllerBase
{
    [HttpGet(Name = "ListChannels")]
    [ProducesResponseType<IReadOnlyList<SalesChannelDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SalesChannelDto>> List(CancellationToken ct) => channelService.ListAsync(ct);

    /// <summary>
    /// Connects a WooCommerce shop with keys entered by hand. The shop is contacted first;
    /// nothing is saved unless the keys work.
    /// </summary>
    [HttpPost("woocommerce", Name = "ConnectWooCommerce")]
    [ProducesResponseType<SalesChannelDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ConnectWooCommerce(ConnectWooCommerceRequest request, CancellationToken ct) =>
        Respond(await channelService.ConnectWooCommerceAsync(request.BaseUrl, request.ConsumerKey, request.ConsumerSecret, ct), created: true);

    /// <summary>Contacts the shop again with the stored keys and updates the channel's status.</summary>
    [HttpPost("{id:guid}/check", Name = "CheckChannel")]
    [ProducesResponseType<SalesChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Check(Guid id, CancellationToken ct) => Respond(await channelService.RecheckAsync(id, ct));

    /// <summary>Replaces the shop's API keys; the new keys must work before they are saved.</summary>
    [HttpPut("{id:guid}/credentials", Name = "ReplaceChannelCredentials")]
    [ProducesResponseType<SalesChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ReplaceCredentials(Guid id, ReplaceCredentialsRequest request, CancellationToken ct) =>
        Respond(await channelService.ReplaceCredentialsAsync(id, request.ConsumerKey, request.ConsumerSecret, ct));

    /// <summary>
    /// Reads the shop's products and shows what an import would do: link to items with the same SKU,
    /// create new items, or skip (with the reason). Nothing is saved.
    /// </summary>
    [HttpGet("{id:guid}/products/preview", Name = "PreviewProductImport")]
    [ProducesResponseType<ProductImportPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> PreviewProducts(Guid id, CancellationToken ct) =>
        Respond(await productImport.PreviewAsync(id, ct));

    /// <summary>
    /// Links shop products to items with the same SKU and creates the chosen new products as items,
    /// with the shop's stock as opening stock. Existing items are not changed.
    /// </summary>
    [HttpPost("{id:guid}/products/import", Name = "ImportProducts")]
    [ProducesResponseType<ProductImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ImportProducts(Guid id, ImportProductsRequest request, CancellationToken ct) =>
        Respond(await productImport.ImportAsync(id, request.CreateExternalIds, ct));

    private IActionResult Respond<T>(ProductImportResult<T> result)
    {
        if (result.NotFound) return NotFound();
        return result.Error is { } error
            ? ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, [error])
            : Ok(result.Value);
    }

    private IActionResult Respond(ChannelResult result, bool created = false)
    {
        if (result.NotFound) return NotFound();
        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
        }

        return created ? StatusCode(StatusCodes.Status201Created, result.Channel) : Ok(result.Channel);
    }
}
