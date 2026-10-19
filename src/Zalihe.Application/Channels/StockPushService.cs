using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Channels;

namespace Zalihe.Application.Channels;

/// <param name="Sent">Products whose stock was sent and accepted by the shop.</param>
/// <param name="Unchanged">Products whose stock changed, but not in whole units, so the shop already shows it.</param>
/// <param name="Rejected">Products the shop refused, e.g. deleted from it; they are tried again on the next push.</param>
public record StockPushResult(int Sent, int Unchanged, int Rejected);

/// <summary>
/// Sends stock to shops (SPEC 7.5): the application is the source of truth, a shop only shows it.
/// Services that change stock call <see cref="MarkChangedAsync"/> before saving, in the same transaction,
/// and <see cref="Schedule"/> after it commits; a background job then calls <see cref="PushAsync"/>.
/// </summary>
public class StockPushService(
    IAppDbContext db,
    ITenantContext tenant,
    ICredentialProtector protector,
    ISalesChannelFactory channels,
    IBackgroundJobs jobs,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Marks the shop products linked to <paramref name="itemIds"/> as changed. Returns the shops to push
    /// to, for <see cref="Schedule"/>. The caller saves the marks together with the stock change.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> MarkChangedAsync(IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var mappings = await db.ItemChannelMappings.Where(m => ids.Contains(m.ItemId)).ToListAsync(ct);
        var now = timeProvider.GetUtcNow();
        foreach (var mapping in mappings)
        {
            mapping.MarkStockChanged(now);
        }

        return mappings.Select(m => m.ChannelId).Distinct().ToList();
    }

    /// <summary>Schedules the push to each shop; call after the marks are committed, so the job sees them.</summary>
    public void Schedule(IEnumerable<Guid> channelIds)
    {
        foreach (var channelId in channelIds.Distinct())
        {
            jobs.PushStock(tenant.TenantId, channelId);
        }
    }

    /// <summary>
    /// Sends the current stock of every changed product to the shop and clears its mark. A product changed
    /// again while this ran keeps its mark (its new change is sent next time); one the shop rejects keeps it too.
    /// Throws <see cref="SalesChannelException"/> when the shop can't be reached; all marks then stay and
    /// the failure is recorded on the channel (SPEC 7.6).
    /// </summary>
    public async Task<StockPushResult> PushAsync(Guid channelId, CancellationToken ct)
    {
        var channel = await db.SalesChannels.SingleOrDefaultAsync(c => c.Id == channelId, ct);
        if (channel is null)
        {
            return new StockPushResult(0, 0, 0); // removed after the job was scheduled
        }

        var changed = await db.ItemChannelMappings
            .Where(m => m.ChannelId == channelId && m.StockChangedAt != null)
            .Join(db.StockLevels, m => m.ItemId, l => l.ItemId, (m, l) => new Changed(
                m.Id, m.ExternalId, m.ParentExternalId, m.StockChangedAt!.Value, m.PushedStock, l.Quantity))
            .ToListAsync(ct);

        // 2,5 kg → 2,3 kg is still 2 in the shop: nothing to send.
        var unchanged = changed.Where(c => c.PushedStock == ItemChannelMapping.ShopStock(c.Stock)).ToList();
        var toSend = changed.Except(unchanged).ToList();

        IReadOnlySet<string> accepted = new HashSet<string>();
        if (toSend.Count > 0)
        {
            try
            {
                accepted = await channels.Create(channel, protector).UpdateStockAsync(
                    toSend.Select(c => new ShopStockUpdate(c.ExternalId, c.ParentExternalId, ItemChannelMapping.ShopStock(c.Stock))).ToList(), ct);
            }
            catch (SalesChannelException e)
            {
                var nowFailing = channel.RecordFailure(e.Code, timeProvider.GetUtcNow());
                await db.SaveChangesAsync(ct);
                if (nowFailing) jobs.EmailChannelFailure(channel.TenantId, channel.Id);
                throw;
            }

            // The shop answered; products it rejected are its own matter, not a broken connection.
            channel.MarkConnected();
            await db.SaveChangesAsync(ct);
        }

        var sent = toSend.Where(c => accepted.Contains(c.ExternalId)).ToList();
        foreach (var done in unchanged.Concat(sent))
        {
            await ClearAsync(done, ct);
        }

        return new StockPushResult(sent.Count, unchanged.Count, toSend.Count - sent.Count);
    }

    /// <summary>Clears the mark only if no change came after the one that was read.</summary>
    private Task<int> ClearAsync(Changed done, CancellationToken ct)
    {
        var shopStock = ItemChannelMapping.ShopStock(done.Stock);
        return db.ItemChannelMappings
            .Where(m => m.Id == done.MappingId && m.StockChangedAt == done.ChangedAt)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.StockChangedAt, (DateTimeOffset?)null)
                .SetProperty(m => m.PushedStock, shopStock), ct);
    }

    private sealed record Changed(
        Guid MappingId, string ExternalId, string? ParentExternalId, DateTimeOffset ChangedAt, int? PushedStock, decimal Stock);
}
