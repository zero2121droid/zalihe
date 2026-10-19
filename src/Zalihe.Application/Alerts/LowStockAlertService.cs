using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Tenants;

namespace Zalihe.Application.Alerts;

public enum LowStockAlertOutcome
{
    /// <summary>No item fell to its minimum since the last email, or it isn't time for one.</summary>
    NotDue,

    Sent,
}

/// <summary>
/// The daily email about items to reorder (SPEC 2.5): active items at or below their minimum, or out of
/// stock, the same as the app's "Ispod minimuma" and "Nema na stanju". See <see cref="LowStockAlert.Check"/>
/// for when it goes out. Runs for the current company; a job calls it every hour for each company.
/// </summary>
public class LowStockAlertService(IAppDbContext db, ITenantContext tenant, IAlertEmails emails, TimeProvider timeProvider)
{
    /// <summary>The companies are in Serbia and the region (SPEC 1); their day is counted by this clock.</summary>
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");

    public async Task<LowStockAlertOutcome> RunAsync(CancellationToken ct)
    {
        // Same conditions as StockStatusRules (Low or OutOfStock), most urgent first like the home screen.
        var low = await (
                from item in db.Items.AsNoTracking()
                where item.IsActive
                join level in db.StockLevels on item.Id equals level.ItemId
                where level.Quantity <= 0 || level.Quantity <= item.MinStock
                orderby level.Quantity <= 0 ? 0 : 1, item.MinStock == 0 ? 1 : level.Quantity / item.MinStock, item.Name
                select new { item.Id, item.Name, item.Sku, item.Unit, Stock = level.Quantity, item.MinStock })
            .ToListAsync(ct);
        var lowIds = low.Select(i => i.Id).ToList();

        var alert = await db.LowStockAlerts.SingleOrDefaultAsync(ct);
        if (alert is null)
        {
            alert = new LowStockAlert(tenant.TenantId);
            db.LowStockAlerts.Add(alert);
        }

        var now = timeProvider.GetUtcNow();
        var news = alert.Check(lowIds, now, Zone).ToHashSet();
        if (news.Count > 0)
        {
            var items = low.Select(i => new LowStockEmailItem(i.Id, i.Name, i.Sku, i.Unit, i.Stock, i.MinStock, news.Contains(i.Id))).ToList();
            // Sent before saving: if sending fails, nothing is marked and the next run tries again.
            await emails.SendLowStockAsync(items, ct);
            alert.MarkSent(lowIds, now);
        }

        await db.SaveChangesAsync(ct);
        return news.Count > 0 ? LowStockAlertOutcome.Sent : LowStockAlertOutcome.NotDue;
    }
}
