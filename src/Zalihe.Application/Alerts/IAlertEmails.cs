using Zalihe.Domain.Items;

namespace Zalihe.Application.Alerts;

/// <param name="IsNew">Not in an earlier email: it fell to its minimum (or ran out) since then.</param>
/// <param name="ShopUrl">The shop's address, e.g. "https://zrno.rs".</param>
/// <param name="ErrorCode">Why it fails, e.g. "channel.unauthorized"; the email explains the known ones.</param>
public record ChannelFailureEmail(string ShopUrl, string? ErrorCode, DateTimeOffset FailingSince);

public record LowStockEmailItem(Guid ItemId, string Name, string Sku, Unit Unit, decimal Stock, decimal MinStock, bool IsNew);

/// <summary>
/// Emails about the current company, to all of its users, each in their own language. Implemented in
/// Infrastructure (texts, SMTP); throws when an email can't be sent, so the job tries again later.
/// </summary>
public interface IAlertEmails
{
    /// <summary>The items to reorder, most urgent first.</summary>
    Task SendLowStockAsync(IReadOnlyList<LowStockEmailItem> items, CancellationToken ct);

    /// <summary>The shop stopped working: orders may not take stock and stock isn't sent to it.</summary>
    Task SendChannelFailureAsync(ChannelFailureEmail email, CancellationToken ct);
}
