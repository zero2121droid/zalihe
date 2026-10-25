using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Channels;
using Zalihe.Web.Auth;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Channels;

/// <summary>
/// Connected online shops. API keys are accepted here but never returned. Everyone sees the shops;
/// connecting and keys are the admin's, checking and importing products also the manager's.
/// </summary>
[ApiController]
[Authorize]
[Route("api/channels")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ChannelsController(ChannelService channelService, ProductImportService productImport) : ControllerBase
{
    [HttpGet(Name = "ListChannels")]
    [ProducesResponseType<IReadOnlyList<SalesChannelDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<SalesChannelDto>> List(CancellationToken ct) => channelService.ListAsync(ct);

    /// <summary>Linked products, orders with unlinked products, the last order and stock waiting to be sent.</summary>
    [HttpGet("{id:guid}/summary", Name = "GetChannelSummary")]
    [ProducesResponseType<ChannelSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ChannelSummaryDto>> Summary(Guid id, CancellationToken ct) =>
        await channelService.GetSummaryAsync(id, ct) is { } summary ? summary : NotFound();

    /// <summary>
    /// Connects a WooCommerce shop with keys entered by hand. The shop is contacted first;
    /// nothing is saved unless the keys work.
    /// </summary>
    [HttpPost("woocommerce", Name = "ConnectWooCommerce")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<SalesChannelDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ConnectWooCommerce(ConnectWooCommerceRequest request, CancellationToken ct) =>
        Respond(await channelService.ConnectWooCommerceAsync(request.BaseUrl, request.ConsumerKey, request.ConsumerSecret, ct), created: true);

    /// <summary>
    /// Starts connecting a WooCommerce shop without copying keys: the client sends the user to
    /// <c>authorizeUrl</c>, where the shop owner approves access, and the shop sends the user back to
    /// <c>/channels?authorization={id}</c>.
    /// </summary>
    [HttpPost("woocommerce/authorizations", Name = "StartWooCommerceAuthorization")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ShopAuthorizationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> StartWooCommerceAuthorization(StartWooCommerceAuthorizationRequest request, CancellationToken ct)
    {
        var result = await channelService.StartWooCommerceAuthorizationAsync(request.BaseUrl, ct);
        return result.Authorization is { } authorization
            ? Ok(authorization)
            : ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
    }

    /// <summary>
    /// Saves the shop once its owner approved access and the keys it sent work. Fails with
    /// <c>channel.authorization_pending</c> while the shop hasn't sent keys, and
    /// <c>channel.authorization_expired</c> when it no longer can.
    /// </summary>
    [HttpPost("woocommerce/authorizations/{id:guid}/complete", Name = "CompleteWooCommerceAuthorization")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<SalesChannelDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> CompleteWooCommerceAuthorization(Guid id, CancellationToken ct) =>
        Respond(await channelService.CompleteWooCommerceAuthorizationAsync(id, ct), created: true);

    /// <summary>Contacts the shop again with the stored keys and updates the channel's status.</summary>
    [HttpPost("{id:guid}/check", Name = "CheckChannel")]
    [Authorize(Policy = Policies.Manager)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<SalesChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Check(Guid id, CancellationToken ct) => Respond(await channelService.RecheckAsync(id, ct));

    /// <summary>Replaces the shop's API keys; the new keys must work before they are saved.</summary>
    [HttpPut("{id:guid}/credentials", Name = "ReplaceChannelCredentials")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
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
    [Authorize(Policy = Policies.Manager)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProductImportPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> PreviewProducts(Guid id, CancellationToken ct) =>
        Respond(await productImport.PreviewAsync(id, ct));

    /// <summary>
    /// Links shop products to items with the same SKU, creates the chosen new products as items, with the
    /// shop's stock as opening stock, and takes the chosen fields (name, barcode, category, prices) from the
    /// shop into linked items. Nothing else of an existing item changes, and stock never comes from the shop.
    /// </summary>
    [HttpPost("{id:guid}/products/import", Name = "ImportProducts")]
    [Authorize(Policy = Policies.Manager)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProductImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ImportProducts(Guid id, ImportProductsRequest request, CancellationToken ct) =>
        Respond(await productImport.ImportAsync(id, request.CreateExternalIds, ct, request.ApplyUpdates));

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
