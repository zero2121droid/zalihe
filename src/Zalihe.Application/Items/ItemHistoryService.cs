using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Application.Stock;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Items;

/// <summary>Which entries of an item's history to show.</summary>
public enum ItemHistoryFilter
{
    All,

    /// <summary>Stock movements only.</summary>
    Stock,

    /// <summary>Changes of the item itself (created, edited, deactivated, activated).</summary>
    Changes,
}

public record ItemFieldChangeDto(string Field, string? OldValue, string? NewValue);

public record ItemChangeDto(ItemChangeKind Kind, IReadOnlyList<ItemFieldChangeDto> Changes);

/// <summary>One history entry: exactly one of <see cref="Movement"/> and <see cref="Change"/> is set.</summary>
public record ItemHistoryEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? UserName,
    StockMovementDto? Movement,
    ItemChangeDto? Change);

/// <summary>
/// Everything that happened to an item, newest first: its stock movements and its own changes,
/// merged and paged in the database.
/// </summary>
public class ItemHistoryService(IAppDbContext db, IUserDirectory users)
{
    public async Task<PagedResult<ItemHistoryEntryDto>?> GetAsync(
        Guid itemId, ItemHistoryFilter filter, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Items.AnyAsync(i => i.Id == itemId, ct))
        {
            return null;
        }

        // Only ids and times are merged (UNION ALL); the page's rows are loaded afterwards.
        var movements = db.StockMovements.Where(m => m.ItemId == itemId)
            .Select(m => new HistoryKey { Id = m.Id, OccurredAt = m.OccurredAt, IsMovement = true });
        var changes = db.ItemChanges.Where(c => c.ItemId == itemId)
            .Select(c => new HistoryKey { Id = c.Id, OccurredAt = c.OccurredAt, IsMovement = false });
        var keys = filter switch
        {
            ItemHistoryFilter.Stock => movements,
            ItemHistoryFilter.Changes => changes,
            _ => movements.Concat(changes),
        };

        var totalCount = await keys.CountAsync(ct);
        var pageKeys = await keys
            .OrderByDescending(k => k.OccurredAt)
            .ThenByDescending(k => k.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var movementIds = pageKeys.Where(k => k.IsMovement).Select(k => k.Id).ToList();
        var changeIds = pageKeys.Where(k => !k.IsMovement).Select(k => k.Id).ToList();
        var movementRows = await db.StockMovements.AsNoTracking().Where(m => movementIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var changeRows = await db.ItemChanges.AsNoTracking().Where(c => changeIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var userIds = movementRows.Values.Select(m => m.UserId)
            .Concat(changeRows.Values.Select(c => c.UserId))
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var names = userIds.Count == 0 ? new Dictionary<Guid, string>() : await users.GetNamesAsync(userIds, ct);
        string? NameOf(Guid? id) => id is { } value && names.TryGetValue(value, out var name) ? name : null;

        var entries = pageKeys.Select(key =>
        {
            if (key.IsMovement)
            {
                var m = movementRows[key.Id];
                var userName = NameOf(m.UserId);
                return new ItemHistoryEntryDto(m.Id, m.OccurredAt, userName, StockService.ToDto(m, userName), null);
            }

            var c = changeRows[key.Id];
            var fields = c.Changes.Select(f => new ItemFieldChangeDto(f.Field, f.OldValue, f.NewValue)).ToList();
            return new ItemHistoryEntryDto(c.Id, c.OccurredAt, NameOf(c.UserId), null, new ItemChangeDto(c.Kind, fields));
        }).ToList();

        return new PagedResult<ItemHistoryEntryDto>(entries, totalCount, page, pageSize);
    }

    private sealed class HistoryKey
    {
        public Guid Id { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
        public bool IsMovement { get; init; }
    }
}
