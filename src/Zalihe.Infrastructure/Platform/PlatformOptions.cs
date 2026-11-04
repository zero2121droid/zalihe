namespace Zalihe.Infrastructure.Platform;

/// <summary>
/// The "Platform" configuration section: who runs Lagerko itself. Not a role, because a company's
/// admin is whoever registered it, while this reaches across all companies.
/// </summary>
public class PlatformOptions
{
    public const string Section = "Platform";

    /// <summary>The addresses told about every new company.</summary>
    public string[] OwnerEmails { get; set; } = [];
}
