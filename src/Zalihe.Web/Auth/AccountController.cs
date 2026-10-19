using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Application.Common;
using Zalihe.Infrastructure.Identity;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Auth;

/// <summary>
/// Settings of the signed-in user and their company. Without roles in v1 every user of the company
/// may rename it; once roles exist (SPEC 11.2) that is the admin's.
/// </summary>
[ApiController]
[Authorize]
[Route("api/account")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class AccountController(AccountService accountService, UserManager<User> userManager, SignInManager<User> signInManager)
    : ControllerBase
{
    /// <summary>Renames the company; returns the signed-in user with the new name.</summary>
    [HttpPut("company", Name = "UpdateCompany")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CurrentUserResponse>> UpdateCompany(UpdateCompanyRequest request, CancellationToken ct)
    {
        if (await userManager.GetUserAsync(User) is not { } user) return Unauthorized();

        await accountService.RenameCompanyAsync(user, request.Name, ct);
        return await CurrentAsync(user, ct);
    }

    /// <summary>Changes the user's name and language; returns the updated user.</summary>
    [HttpPut("profile", Name = "UpdateProfile")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CurrentUserResponse>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
    {
        if (await userManager.GetUserAsync(User) is not { } user) return Unauthorized();

        var errors = await accountService.UpdateProfileAsync(user, request.Name, request.Language);
        if (errors.Count > 0) return Failed(errors);

        return await CurrentAsync(user, ct);
    }

    /// <summary>
    /// Changes the password. This device stays signed in; other devices are signed out once their
    /// cookie is checked again (Identity's security stamp).
    /// </summary>
    [HttpPost("password", Name = "ChangePassword")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (await userManager.GetUserAsync(User) is not { } user) return Unauthorized();

        var errors = await accountService.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (errors.Count > 0) return Failed(errors);

        // The new security stamp would sign this device out as well; issue it a fresh cookie.
        await signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

    private async Task<CurrentUserResponse> CurrentAsync(User user, CancellationToken ct)
    {
        var tenant = await accountService.GetTenantAsync(user.TenantId, ct);
        return new CurrentUserResponse(user.Id, user.Name, user.Email!, user.Language, tenant.Id, tenant.Name);
    }

    private ObjectResult Failed(IReadOnlyList<AppError> errors) =>
        ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, errors);
}
