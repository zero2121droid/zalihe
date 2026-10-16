using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Zalihe.Domain.Channels;
using Zalihe.Domain.Items;
using Zalihe.Domain.Orders;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Common;

/// <summary>
/// Data access for application services. Implemented by the EF Core context in Infrastructure.
/// Sets of company data are already filtered to the current tenant.
/// </summary>
public interface IAppDbContext
{
    DbSet<Item> Items { get; }
    DbSet<ItemChange> ItemChanges { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockLevel> StockLevels { get; }
    DbSet<SalesChannel> SalesChannels { get; }
    DbSet<ItemChannelMapping> ItemChannelMappings { get; }
    DbSet<ExternalOrder> ExternalOrders { get; }

    /// <summary>For explicit transactions, e.g. around locking stock.</summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Loads an item's stock and locks it until the current transaction ends, so concurrent
    /// movements for the same item run one after another instead of overwriting each other.
    /// Must be called inside a transaction.
    /// </summary>
    Task<StockLevel?> LockStockLevelAsync(Guid itemId, CancellationToken ct);

    /// <summary>
    /// Loads a shop order with its lines and locks it until the current transaction ends, so two
    /// events for the same order are processed one after another. Must be called inside a transaction.
    /// </summary>
    Task<ExternalOrder?> LockExternalOrderAsync(Guid channelId, string externalId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
