using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Channels;
using Zalihe.Application.Common;
using Zalihe.Domain.Channels;
using Zalihe.Infrastructure.Tenancy;

namespace Zalihe.Web.Webhooks;

/// <summary>
/// Receives the keys a WooCommerce shop sends after its owner approved access (wc-auth, SPEC 7.1).
/// The caller is the shop, not a user: no sign-in or antiforgery token, the token from the start of
/// the connection proves which one it is, and the endpoint is left out of the OpenAPI document.
/// Anything but 200 makes the shop show its owner an error and delete the keys. The route matches
/// <see cref="ShopAuthorizationSettings.WooCommerceCallbackUrl"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("webhooks/woo/authorize")]
public class WooCommerceAuthCallbackController(IAppDbContext db, TenantContext tenant, ChannelService channels) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Receive(WooCommerceKeysCallback keys, CancellationToken ct)
    {
        if (await db.FindShopAuthorizationTenantAsync(ShopAuthorization.HashToken(keys.UserId), ct) is not { } tenantId)
        {
            return NotFound();
        }

        tenant.Set(tenantId);
        return await channels.ReceiveWooCommerceKeysAsync(keys.UserId, keys.ConsumerKey, keys.ConsumerSecret, ct)
            ? Ok()
            : BadRequest();
    }
}

/// <summary>The body WooCommerce posts; <c>user_id</c> is the token the application gave the shop.</summary>
public record WooCommerceKeysCallback(
    [property: JsonPropertyName("user_id")][Required][MaxLength(100)] string UserId,
    [property: JsonPropertyName("consumer_key")][Required][MaxLength(200)] string ConsumerKey,
    [property: JsonPropertyName("consumer_secret")][Required][MaxLength(200)] string ConsumerSecret);
