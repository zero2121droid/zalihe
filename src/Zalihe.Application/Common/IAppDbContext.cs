using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Common;

/// <summary>
/// Data access for application services. Implemented by the EF Core context in Infrastructure.
/// Sets of company data are already filtered to the current tenant.
/// </summary>
public interface IAppDbContext
{
    DbSet<Item> Items { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockLevel> StockLevels { get; }

    /// <summary>For explicit transactions, e.g. around locking stock.</summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Loads an item's stock and locks it until the current transaction ends, so concurrent
    /// movements for the same item run one after another instead of overwriting each other.
    /// Must be called inside a transaction.
    /// </summary>
    Task<StockLevel?> LockStockLevelAsync(Guid itemId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
