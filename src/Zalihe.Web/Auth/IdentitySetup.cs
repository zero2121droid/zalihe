using Microsoft.AspNetCore.Identity;
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

        // The cookie's claims (company and role) are rebuilt from the user every minute instead of every 30,
        // so a changed role applies soon without signing in again. A changed security stamp (new password)
        // signs the user out at the same check.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        // Password reset links (the only Identity tokens in use) are valid for two hours.
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "zalihe.auth";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = CookieAntiforgeryFilter.HeaderName;
            // Unlike the sign-in cookie, the antiforgery cookie isn't Secure by default: Secure over HTTPS.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        return services;
    }
}
