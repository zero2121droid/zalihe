using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Platform;

namespace Zalihe.Web.Auth;

/// <summary>
/// Who may call what (SPEC 11.2), as authorization policies on the role claim. Endpoints without a
/// policy are for every user of the company (items, stock movements, orders, own account).
/// </summary>
public static class Policies
{
    /// <summary>Company settings, users and their roles, connecting the shop.</summary>
    public const string Admin = "Admin";

    /// <summary>Imports, reports, checking the shop and stock maintenance: admins and managers.</summary>
    public const string Manager = "Manager";

    /// <summary>
    /// The page about all companies (SPEC 11.8). Not a role: every company has its admin, while this
    /// is whoever "Platform:OwnerEmails" names.
    /// </summary>
    public const string PlatformOwner = "PlatformOwner";

    public static IServiceCollection AddAppPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Admin, policy => policy.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(Manager, policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)))
            .AddPolicy(PlatformOwner, policy => policy.RequireAuthenticatedUser().AddRequirements(new PlatformOwnerRequirement()));
        services.AddSingleton<IAuthorizationHandler, PlatformOwnerHandler>();
        return services;
    }
}

public class PlatformOwnerRequirement : IAuthorizationRequirement;

/// <summary>Reads the configuration on every request, so removing an address applies at the next restart without a new sign-in.</summary>
public class PlatformOwnerHandler(IOptions<PlatformOptions> options) : AuthorizationHandler<PlatformOwnerRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformOwnerRequirement requirement)
    {
        if (options.Value.IsOwner(context.User.FindFirstValue(ClaimTypes.Email)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
