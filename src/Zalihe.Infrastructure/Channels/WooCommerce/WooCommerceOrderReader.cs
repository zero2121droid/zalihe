using System.Globalization;
using System.Text.Json;
using Zalihe.Application.Channels;
using Zalihe.Domain.Channels;

namespace Zalihe.Infrastructure.Channels.WooCommerce;

/// <summary>
/// Reads a WooCommerce order (REST API v3 format, as in webhooks and /orders). Only the order's ID,
/// status, creation and change times and line products and quantities are read; billing, shipping, customer and
/// payment fields are never touched, so they can't end up stored anywhere.
/// </summary>
public class WooCommerceOrderReader : IShopOrderReader
{
    public ShopOrder? ReadWebhook(SalesChannelType type, byte[] body)
    {
        if (type != SalesChannelType.WooCommerce) throw new ArgumentOutOfRangeException(nameof(type));

        try
        {
            using var document = JsonDocument.Parse(body);
            return Read(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The order, or null when a field stock depends on is missing or has an unexpected type.</summary>
    public static ShopOrder? Read(JsonElement order)
    {
        if (order.ValueKind != JsonValueKind.Object
            || Long(order, "id") is not { } id || id <= 0
            || Text(order, "status") is not { } status
            || Utc(order, "date_created_gmt") is not { } createdAt
            || Utc(order, "date_modified_gmt") is not { } modifiedAt
            || !order.TryGetProperty("line_items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var lines = new List<ShopOrderLine>();
        foreach (var item in items.EnumerateArray())
        {
            // A variation's stock is the variation's own; product_id is then its variable product.
            var productId = Long(item, "variation_id") is > 0 ? Long(item, "variation_id")!.Value : Long(item, "product_id") ?? 0;
            if (productId <= 0) continue; // the product was deleted from the shop

            if (!item.TryGetProperty("quantity", out var quantity) || quantity.ValueKind != JsonValueKind.Number
                || !quantity.TryGetDecimal(out var value) || value < 0)
            {
                return null;
            }

            lines.Add(new ShopOrderLine(productId.ToString(CultureInfo.InvariantCulture), value, Text(item, "name")));
        }

        return new ShopOrder(id.ToString(CultureInfo.InvariantCulture), status, createdAt, modifiedAt, lines);
    }

    private static long? Long(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text.Trim()
            : null;

    /// <summary>The "_gmt" dates are UTC without an offset, e.g. "2026-10-16T09:00:00".</summary>
    private static DateTimeOffset? Utc(JsonElement order, string name) =>
        DateTimeOffset.TryParse(Text(order, name), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? value
            : null;
}
