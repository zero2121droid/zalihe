using Microsoft.EntityFrameworkCore;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Common;

/// <summary>
/// Data access for application services. Implemented by the EF Core context in Infrastructure.
/// Sets of company data are already filtered to the current tenant.
/// </summary>
public interface IAppDbContext
{
    DbSet<Item> Items { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
