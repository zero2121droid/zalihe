using Microsoft.AspNetCore.Identity;
using Zalihe.Infrastructure.Identity;
using Zalihe.Infrastructure.Tenancy;

namespace Zalihe.Web.Tenancy;

/// <summary>Sets the current tenant from the signed-in user's "tenant_id" claim.</summary>
public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, UserManager<User> userManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst(AppClaims.TenantId)?.Value;
            if (Guid.TryParse(claim, out var tenantId))
            {
                tenantContext.Set(tenantId);
            }
            else if (await userManager.GetUserAsync(context.User) is { } user)
            {
                // Sessions created before the claim existed; the claim is added on next sign-in.
                tenantContext.Set(user.TenantId);
            }
        }

        await next(context);
    }
}
