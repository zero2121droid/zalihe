using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Zalihe.Infrastructure.Identity;
using Zalihe.Web.Errors;

namespace Zalihe.Web.Auth;

/// <summary>
/// The invitation link's page, for someone who has no account yet: no sign-in. The token in the address
/// is the only key, 256 random bits, used once and valid for a week.
/// </summary>
[ApiController]
[Route("api/invitations")]
public class InvitationsController(InvitationService invitations, AccountService accountService, SignInManager<User> signInManager)
    : ControllerBase
{
    /// <summary>The company and email an invitation is for; 404 when it is unknown, used, revoked or expired.</summary>
    [HttpGet("{token}", Name = "GetInvitation")]
    [ProducesResponseType<InvitationInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(string token, CancellationToken ct) =>
        await invitations.GetByTokenAsync(token, ct) is { } info
            ? Ok(info)
            : ApiProblems.Result(HttpContext, StatusCodes.Status404NotFound, AccountService.InvitationInvalid);

    /// <summary>Creates the account in the inviting company and signs the new user in.</summary>
    [HttpPost("{token}/accept", Name = "AcceptInvitation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Accept(string token, AcceptInvitationRequest request, CancellationToken ct)
    {
        var result = await accountService.AcceptInvitationAsync(token, request.Name, request.Password, request.Language, ct);
        if (!result.Succeeded)
        {
            return ApiProblems.Result(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationFailed, result.Errors);
        }

        await signInManager.SignInAsync(result.User!, isPersistent: false);
        return NoContent();
    }
}
