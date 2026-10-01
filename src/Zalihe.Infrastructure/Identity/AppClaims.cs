using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Zalihe.Infrastructure.Identity;

public static class AppClaims
{
    public const string TenantId = "tenant_id";
}

/// <summary>Adds the user's company to the sign-in cookie (and later to bearer tokens).</summary>
public class AppClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaims.TenantId, user.TenantId.ToString()));
        return identity;
    }
}
