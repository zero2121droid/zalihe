using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Zalihe.Infrastructure.Identity;

public static class AppClaims
{
    public const string TenantId = "tenant_id";
}

/// <summary>
/// Adds the user's company and role to the sign-in cookie (and later to bearer tokens). The role is the
/// standard role claim, so <c>[Authorize(Roles = …)]</c> and policies read it. It is refreshed when the
/// cookie's security stamp is checked (every minute, see IdentitySetup), so a changed role applies
/// without signing in again.
/// </summary>
public class AppClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaims.TenantId, user.TenantId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        return identity;
    }
}
