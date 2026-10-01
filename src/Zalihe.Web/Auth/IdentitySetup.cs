using Zalihe.Infrastructure.Identity;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Web.Auth;

public static class IdentitySetup
{
    public static IServiceCollection AddAppIdentity(this IServiceCollection services)
    {
        // Registers the combined bearer + cookie scheme (IdentityConstants.BearerAndApplicationScheme)
        // as default, so the mobile app can use bearer tokens later. In v1 only the cookie is issued.
        services
            .AddIdentityApiEndpoints<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "zalihe.auth";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        services.AddAntiforgery(options => options.HeaderName = CookieAntiforgeryFilter.HeaderName);

        return services;
    }
}
