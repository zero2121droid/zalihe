using Microsoft.AspNetCore.Identity;
using Zalihe.Domain.Users;

namespace Zalihe.Infrastructure.Identity;

public class User : IdentityUser<Guid>
{
    public const int NameMaxLength = 100;

    /// <summary>Display name shown in the app, e.g. "Miljan".</summary>
    public string Name { get; set; } = "";
    public Guid TenantId { get; set; }
    public string Language { get; set; } = Languages.Default;
}
