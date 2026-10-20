using System.ComponentModel.DataAnnotations;
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
public class AccountController(
    AccountService accountService,
    InvitationService invitations,
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    ILogger<AccountController> logger)
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

    /// <summary>The company's users.</summary>
    [HttpGet("users", Name = "ListCompanyUsers")]
    [ProducesResponseType<PagedResult<CompanyUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<CompanyUserDto>> Users(
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        invitations.ListUsersAsync(page, pageSize, ct);

    /// <summary>Invitations sent and not yet accepted or revoked, newest first; expired ones can be sent again.</summary>
    [HttpGet("invitations", Name = "ListInvitations")]
    [ProducesResponseType<PagedResult<InvitationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedResult<InvitationDto>> Invitations(
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "validation.out_of_range")] int page = 1,
        [FromQuery, Range(1, Paging.MaxPageSize, ErrorMessage = "validation.out_of_range")] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        invitations.ListInvitationsAsync(page, pageSize, ct);

    /// <summary>
    /// Invites someone to the company by email; inviting the same address again sends a new link (the old one
    /// stops working). 503 with "email.unavailable" when the email can't be sent: nothing is saved then.
    /// </summary>
    [HttpPost("invitations", Name = "Invite")]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Invite(InviteRequest request, CancellationToken ct)
    {
        if (await userManager.GetUserAsync(User) is not { } user) return Unauthorized();

        InviteResult result;
        try
        {
            result = await invitations.InviteAsync(user, request.Email, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Sending an invitation failed");
            return ApiProblems.Result(HttpContext, StatusCodes.Status503ServiceUnavailable, "email.unavailable");
        }

        return result.Succeeded ? Ok(result.Invitation) : Failed([result.Error!]);
    }

    /// <summary>Withdraws an invitation; its link stops working.</summary>
    [HttpDelete("invitations/{id:guid}", Name = "RevokeInvitation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken ct) =>
        await invitations.RevokeAsync(id, ct) ? NoContent() : NotFound();

    private async Task<CurrentUserResponse> CurrentAsync(User user, CancellationToken ct)
    {
        var tenant = await accountService.GetTenantAsync(user.TenantId, ct);
        return new CurrentUserResponse(user.Id, user.Name, user.Email!, user.Language, tenant.Id, tenant.Name);
    }

    private ObjectResult Failed(IReadOnlyList<AppError> errors) =>
        ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, errors);
}
