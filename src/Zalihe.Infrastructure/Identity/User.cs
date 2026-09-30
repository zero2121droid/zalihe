using Microsoft.AspNetCore.Identity;
using Zalihe.Domain.Users;

namespace Zalihe.Infrastructure.Identity;

public class User : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public string Language { get; set; } = Languages.Default;
}
