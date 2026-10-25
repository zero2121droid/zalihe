using System.Security.Claims;
using Zalihe.Application.Common;
using Zalihe.Domain.Users;

namespace Zalihe.Web.Auth;

/// <summary>The signed-in user from the request's claims (cookie now, bearer token later).</summary>
public class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;
}
