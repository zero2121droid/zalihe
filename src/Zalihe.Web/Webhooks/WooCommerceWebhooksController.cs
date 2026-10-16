using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Application.Webhooks;
using Zalihe.Infrastructure.Tenancy;

namespace Zalihe.Web.Webhooks;

/// <summary>
/// Receives WooCommerce webhooks (SPEC 7.3). The caller is the shop, not a user: there is no sign-in
/// or antiforgery token, the signature proves the sender, and the endpoint is left out of the OpenAPI
/// document (no client calls it). The route matches <see cref="Application.Channels.WebhookSettings"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("webhooks/woo/{channelId:guid}")]
public class WooCommerceWebhooksController(IAppDbContext db, TenantContext tenant, WebhookService webhooks) : ControllerBase
{
    /// <summary>An order with hundreds of lines is far below this.</summary>
    private const int MaxBodyBytes = 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Receive(Guid channelId, CancellationToken ct)
    {
        var signature = Request.Headers["X-WC-Webhook-Signature"].ToString();

        // When a webhook is created, WooCommerce sends an unsigned form "webhook_id=…" and expects a success.
        // MVC has already read a form body for model binding, so the ping is recognized from the form.
        if (signature.Length == 0 && Request.HasFormContentType && (await Request.ReadFormAsync(ct)).ContainsKey("webhook_id"))
        {
            return Ok();
        }

        // The signature is computed over the exact bytes, so the body is read as it came.
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();

        if (await db.FindChannelTenantAsync(channelId, ct) is not { } tenantId)
        {
            return NotFound();
        }

        tenant.Set(tenantId);
        var outcome = await webhooks.ReceiveAsync(channelId, Request.Headers["X-WC-Webhook-Topic"].ToString(), signature, body, ct);
        return outcome switch
        {
            WebhookOutcome.InvalidSignature => Unauthorized(),
            WebhookOutcome.NotFound => NotFound(),
            _ => Ok(),
        };
    }
}
