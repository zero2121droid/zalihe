using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Infrastructure.Identity;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AccountService accountService,
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>Registers a company with its owner and signs the owner in.</summary>
    [HttpPost("register", Name = "Register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await accountService.RegisterAsync(
            new RegisterCommand(request.CompanyName, request.Name, request.Email, request.Password, request.Language), ct);
        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
        }

        await signInManager.SignInAsync(result.User!, isPersistent: false);
        return NoContent();
    }

    [HttpPost("login", Name = "Login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.Email.Trim(), request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status401Unauthorized, "auth.locked_out");
        }

        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status401Unauthorized, "auth.invalid_credentials");
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout", Name = "Logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>Returns the signed-in user and issues the antiforgery token (XSRF-TOKEN cookie).</summary>
    [Authorize]
    [HttpGet("me", Name = "GetCurrentUser")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var tenant = await accountService.GetTenantAsync(user.TenantId, ct);
        CookieAntiforgeryFilter.IssueToken(antiforgery, HttpContext);

        return new CurrentUserResponse(user.Id, user.Name, user.Email!, user.Language, tenant.Id, tenant.Name);
    }
}
