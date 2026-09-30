using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Auth;

/// <summary>
/// Validates the antiforgery token on state-changing requests authenticated by the cookie.
/// Anonymous and bearer token requests are not affected, since browsers don't send those automatically.
/// </summary>
public class CookieAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    /// <summary>Readable by JavaScript; the frontend copies it into <see cref="HeaderName"/>.</summary>
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var method = httpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        var cookieAuth = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!cookieAuth.Succeeded)
        {
            return;
        }

        if (!await antiforgery.IsRequestValidAsync(httpContext))
        {
            context.Result = ApiProblems.Result(httpContext, StatusCodes.Status400BadRequest, "auth.antiforgery_invalid");
        }
    }

    /// <summary>Issues a token for the current user. Call after the user is known (e.g. on "me").</summary>
    public static void IssueToken(IAntiforgery antiforgery, HttpContext httpContext)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        httpContext.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = httpContext.Request.IsHttps,
            Path = "/",
        });
    }
}
