namespace Zalihe.Infrastructure.Platform;

/// <summary>
/// The "Platform" configuration section: who runs Lagerko itself. Not a role, because a company's
/// admin is whoever registered it, while this reaches across all companies.
/// </summary>
public class PlatformOptions
{
    public const string Section = "Platform";

    /// <summary>
    /// The addresses told about every new company, whose accounts may see all companies. Registration
    /// doesn't verify addresses, so an address goes here only after its account exists (DEPLOY 4).
    /// </summary>
    public string[] OwnerEmails { get; set; } = [];

    public bool IsOwner(string? email) => OwnerEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
}
