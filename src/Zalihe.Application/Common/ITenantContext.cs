namespace Zalihe.Application.Common;

/// <summary>
/// The company the current operation works for. Set from the signed-in user's claim for
/// HTTP requests; webhooks and background jobs will set it explicitly.
/// </summary>
public interface ITenantContext
{
    bool HasTenant { get; }

    /// <summary>Throws when no tenant is set, so company data is never read without one.</summary>
    Guid TenantId { get; }
}
