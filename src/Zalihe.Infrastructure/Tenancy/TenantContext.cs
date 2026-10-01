using Zalihe.Application.Common;

namespace Zalihe.Infrastructure.Tenancy;

/// <summary>Scoped holder of the current tenant. Set once per request or job.</summary>
public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public bool HasTenant => _tenantId is not null;

    public Guid TenantId => _tenantId ?? throw new InvalidOperationException("No tenant is set for this operation.");

    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id is empty.", nameof(tenantId));
        if (_tenantId is not null && _tenantId != tenantId)
        {
            throw new InvalidOperationException("The tenant is already set to a different company.");
        }

        _tenantId = tenantId;
    }
}
