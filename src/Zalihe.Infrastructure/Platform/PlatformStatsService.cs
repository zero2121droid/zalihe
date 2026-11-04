using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zalihe.Application.Common;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Infrastructure.Platform;

/// <param name="ActiveLast7Days">Companies with at least one stock movement in the last 7 days, from the shop or by hand.</param>
public record PlatformSummaryDto(int Companies, int NewLast7Days, int ActiveLast7Days, int WithShop);

/// <param name="IsOwn">A company of the platform's owner: listed, but left out of the summary.</param>
public record PlatformCompanyDto(
    Guid Id,
    string Name,
    DateTimeOffset RegisteredAt,
    int ActiveItems,
    bool HasShop,
    DateTimeOffset? LastMovementAt,
    bool IsOwn);

public record PlatformCompanyListQuery(string? Search = null, int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>
/// How the companies use Lagerko, for the platform's owner (SPEC 11.8). The one place that reads across
/// companies for a person, so the tenant filters are turned off here. Only counts and dates leave it,
/// never a company's items, orders or prices.
/// </summary>
public class PlatformStatsService(AppDbContext db, IOptions<PlatformOptions> options, TimeProvider timeProvider)
{
    public async Task<PlatformSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var own = await OwnCompanyIdsAsync(ct);
        var channels = db.SalesChannels.IgnoreQueryFilters();
        var movements = db.StockMovements.IgnoreQueryFilters();
        var since = timeProvider.GetUtcNow().AddDays(-7);
        var companies = db.Tenants.Where(t => !own.Contains(t.Id));

        return new PlatformSummaryDto(
            await companies.CountAsync(ct),
            await companies.CountAsync(t => t.CreatedAt >= since, ct),
            await companies.CountAsync(t => movements.Any(m => m.TenantId == t.Id && m.OccurredAt >= since), ct),
            await companies.CountAsync(t => channels.Any(c => c.TenantId == t.Id), ct));
    }

    /// <summary>All companies, newest first; searchable by name.</summary>
    public async Task<PagedResult<PlatformCompanyDto>> ListCompaniesAsync(PlatformCompanyListQuery query, CancellationToken ct)
    {
        var own = await OwnCompanyIdsAsync(ct);
        var items = db.Items.IgnoreQueryFilters();
        var channels = db.SalesChannels.IgnoreQueryFilters();
        var movements = db.StockMovements.IgnoreQueryFilters();

        var companies = db.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            companies = companies.Where(t => t.Name.ToLower().Contains(term));
        }

        var totalCount = await companies.CountAsync(ct);
        var page = await companies
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new PlatformCompanyDto(
                t.Id,
                t.Name,
                t.CreatedAt,
                items.Count(i => i.TenantId == t.Id && i.IsActive),
                channels.Any(c => c.TenantId == t.Id),
                movements.Where(m => m.TenantId == t.Id).Max(m => (DateTimeOffset?)m.OccurredAt),
                own.Contains(t.Id)))
            .ToListAsync(ct);

        return new PagedResult<PlatformCompanyDto>(page, totalCount, query.Page, query.PageSize);
    }

    private async Task<List<Guid>> OwnCompanyIdsAsync(CancellationToken ct)
    {
        var emails = options.Value.OwnerEmails.Select(e => e.ToUpperInvariant()).ToList();
        return await db.Users
            .Where(u => u.NormalizedEmail != null && emails.Contains(u.NormalizedEmail))
            .Select(u => u.TenantId)
            .Distinct()
            .ToListAsync(ct);
    }
}
