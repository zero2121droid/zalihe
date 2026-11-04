using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Zalihe.Application.Common;
using Zalihe.Infrastructure.Identity;
using Zalihe.Infrastructure.Platform;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AccountService accountService,
    IOptions<PlatformOptions> platform,
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>
    /// Starts a registration and emails a code to the address. The company exists only after
    /// the code is confirmed with the token returned here.
    /// </summary>
    [HttpPost("register", Name = "Register")]
    [EnableRateLimiting(RegistrationRateLimit.Policy)]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await accountService.StartRegistrationAsync(
            new RegisterCommand(request.CompanyName, request.Name, request.Email, request.Password, request.Language), ct);
        return result.Token is { } token ? new RegisterResponse(token) : RegistrationProblem(result.Errors);
    }

    /// <summary>Checks the emailed code, creates the company with its owner and signs the owner in.</summary>
    [HttpPost("register/confirm", Name = "ConfirmRegistration")]
    [EnableRateLimiting(RegistrationRateLimit.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> ConfirmRegistration(ConfirmRegistrationRequest request, CancellationToken ct)
    {
        var result = await accountService.ConfirmRegistrationAsync(request.Token, request.Code, ct);
        if (!result.Succeeded)
        {
            return RegistrationProblem(result.Errors);
        }

        await signInManager.SignInAsync(result.User!, isPersistent: false);
        return NoContent();
    }

    /// <summary>Emails a new code; the previous one stops working.</summary>
    [HttpPost("register/resend", Name = "ResendRegistrationCode")]
    [EnableRateLimiting(RegistrationRateLimit.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> ResendRegistrationCode(ResendRegistrationCodeRequest request, CancellationToken ct)
    {
        var errors = await accountService.ResendRegistrationCodeAsync(request.Token, ct);
        return errors.Count == 0 ? NoContent() : RegistrationProblem(errors);
    }

    private ObjectResult RegistrationProblem(IReadOnlyList<AppError> errors) =>
        errors.Any(e => e.Code == AccountService.EmailUnavailable)
            ? ApiProblems.Result(HttpContext, StatusCodes.Status503ServiceUnavailable, AccountService.EmailUnavailable)
            : ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, errors);

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

    /// <summary>
    /// Emails a link for setting a new password. Always 204, whether or not the address has an account,
    /// so the form doesn't reveal which addresses are registered.
    /// </summary>
    [HttpPost("forgot-password", Name = "ForgotPassword")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await accountService.RequestPasswordResetAsync(request.Email, ct);
        return NoContent();
    }

    /// <summary>Sets a new password with the emailed link's token and signs the user in.</summary>
    [HttpPost("reset-password", Name = "ResetPassword")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await accountService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
        }

        await signInManager.SignInAsync(result.User!, isPersistent: false);
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

        return CurrentUserResponse.For(user, tenant, platform.Value);
    }
}
