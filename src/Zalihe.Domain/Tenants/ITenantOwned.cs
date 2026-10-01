namespace Zalihe.Domain.Tenants;

/// <summary>
/// Marks company data. Every such entity gets the TenantId global query filter automatically,
/// so queries never filter by tenant by hand.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
