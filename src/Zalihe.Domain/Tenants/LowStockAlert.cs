namespace Zalihe.Domain.Tenants;

/// <summary>
/// What the company was last told about items to reorder (SPEC 2.5): the low items already in an
/// email, and when the last one went out. One per company.
/// </summary>
public class LowStockAlert : ITenantOwned
{
    /// <summary>Emails go out only in the daytime, by the company's clock.</summary>
    public const int FirstHour = 7;
    public const int LastHour = 21;

    public Guid TenantId { get; private set; }

    /// <summary>Items that are low and were already in an email. An item that recovers is dropped, so falling again is news.</summary>
    public List<Guid> ReportedItemIds { get; private set; } = [];

    public DateTimeOffset? LastSentAt { get; private set; }

    public LowStockAlert(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        TenantId = tenantId;
    }

    // For EF Core.
    private LowStockAlert()
    {
    }

    /// <summary>
    /// Decides whether to email now, given the items that are low at this moment. An email goes out
    /// when at least one of them wasn't in an earlier email, at most once per calendar day and only
    /// between <see cref="FirstHour"/> and <see cref="LastHour"/> in <paramref name="zone"/>. Items
    /// that recovered are forgotten either way. Call <see cref="MarkSent"/> once the email is sent.
    /// </summary>
    /// <returns>The items new since the last email when one should go out now; otherwise empty.</returns>
    public IReadOnlyList<Guid> Check(IReadOnlyCollection<Guid> lowNow, DateTimeOffset now, TimeZoneInfo zone)
    {
        var low = lowNow.ToHashSet();
        ReportedItemIds = ReportedItemIds.Where(low.Contains).ToList();

        var reported = ReportedItemIds.ToHashSet();
        var news = lowNow.Where(id => !reported.Contains(id)).Distinct().ToList();
        if (news.Count == 0) return [];

        var local = TimeZoneInfo.ConvertTime(now, zone);
        if (local.Hour < FirstHour || local.Hour >= LastHour) return [];
        if (LastSentAt is { } last && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(last, zone).Date) == DateOnly.FromDateTime(local.Date)) return [];

        return news;
    }

    /// <summary>The email listing <paramref name="lowNow"/> went out at <paramref name="sentAt"/>.</summary>
    public void MarkSent(IReadOnlyCollection<Guid> lowNow, DateTimeOffset sentAt)
    {
        ReportedItemIds = lowNow.Distinct().ToList();
        LastSentAt = sentAt;
    }
}
