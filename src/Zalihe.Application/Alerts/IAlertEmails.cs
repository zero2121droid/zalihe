using Zalihe.Domain.Items;

namespace Zalihe.Application.Alerts;

/// <param name="IsNew">Not in an earlier email: it fell to its minimum (or ran out) since then.</param>
public record LowStockEmailItem(Guid ItemId, string Name, string Sku, Unit Unit, decimal Stock, decimal MinStock, bool IsNew);

/// <summary>
/// Emails about the current company, to all of its users, each in their own language. Implemented in
/// Infrastructure (texts, SMTP); throws when an email can't be sent, so the job tries again later.
/// </summary>
public interface IAlertEmails
{
    /// <summary>The items to reorder, most urgent first.</summary>
    Task SendLowStockAsync(IReadOnlyList<LowStockEmailItem> items, CancellationToken ct);
}
