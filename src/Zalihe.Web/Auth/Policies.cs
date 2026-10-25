using Zalihe.Domain.Users;

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

    public static IServiceCollection AddAppPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Admin, policy => policy.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(Manager, policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        return services;
    }
}
