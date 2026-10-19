using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Zalihe.Domain.Channels;
using Zalihe.Domain.Items;
using Zalihe.Domain.Orders;
using Zalihe.Domain.Stock;
using Zalihe.Domain.Tenants;

namespace Zalihe.Application.Common;

/// <summary>
/// Data access for application services. Implemented by the EF Core context in Infrastructure.
/// Sets of company data are already filtered to the current tenant.
/// </summary>
public interface IAppDbContext
{
    DbSet<Item> Items { get; }
    DbSet<ItemChange> ItemChanges { get; }
    DbSet<ItemImage> ItemImages { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockLevel> StockLevels { get; }
    DbSet<SalesChannel> SalesChannels { get; }
    DbSet<ItemChannelMapping> ItemChannelMappings { get; }
    DbSet<ExternalOrder> ExternalOrders { get; }
    DbSet<WebhookEvent> WebhookEvents { get; }
    DbSet<LowStockAlert> LowStockAlerts { get; }

    /// <summary>For explicit transactions, e.g. around locking stock.</summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Loads an item's stock and locks it until the current transaction ends, so concurrent
    /// movements for the same item run one after another instead of overwriting each other.
    /// Must be called inside a transaction.
    /// </summary>
    Task<StockLevel?> LockStockLevelAsync(Guid itemId, CancellationToken ct);

    /// <summary>
    /// Loads the shop order with <paramref name="candidate"/>'s shop and number, saving the candidate first
    /// when there is none, and locks it until the current transaction ends. Two events for the same
    /// order, even a new one, are therefore processed one after another. Must be called inside a transaction.
    /// </summary>
    Task<ExternalOrder> LockOrAddExternalOrderAsync(ExternalOrder candidate, CancellationToken ct);

    /// <summary>
    /// The company a shop belongs to, read without a current company: a webhook arrives knowing only
    /// its shop, and the company is set from this. Crosses companies, like the three methods below;
    /// nothing else may.
    /// </summary>
    Task<Guid?> FindChannelTenantAsync(Guid channelId, CancellationToken ct);

    /// <summary>Every company's shops, for background jobs that then work on each shop with its company set.</summary>
    Task<IReadOnlyList<ChannelOwner>> ListAllChannelsAsync(CancellationToken ct);

    /// <summary>Every company, for background jobs that then work on each one with its company set.</summary>
    Task<IReadOnlyList<Guid>> ListAllTenantIdsAsync(CancellationToken ct);

    /// <summary>Deletes every company's webhook events received before <paramref name="before"/>; returns how many.</summary>
    Task<int> DeleteWebhookEventsReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public record ChannelOwner(Guid TenantId, Guid ChannelId);
