using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Zalihe.Application.Channels;

namespace Zalihe.Infrastructure.Channels.WooCommerce;

/// <summary>
/// WooCommerce REST API v3 client for one shop. Authenticates with the consumer key and secret
/// (HTTP Basic, which WooCommerce accepts over HTTPS) and turns failures into channel error codes.
/// </summary>
public class WooCommerceClient(HttpClient http, string baseUrl, WooCommerceCredentials credentials) : ISalesChannel
{
    private const int PageSize = 100;
    private const string ProductFields = "id,name,type,sku,regular_price,price,stock_quantity,cost_of_goods_sold,categories,images";
    private const string VariationFields = "id,sku,regular_price,price,stock_quantity,cost_of_goods_sold,attributes,image";

    /// <summary>Only what stock needs: the shop doesn't even send the customer's data.</summary>
    private const string OrderFields = "id,status,date_created_gmt,date_modified_gmt,line_items";

    /// <summary>The same topics <see cref="Application.Webhooks.WebhookService"/> accepts.</summary>
    private static readonly string[] WebhookTopics = ["order.created", "order.updated"];

    public async Task CheckConnectionAsync(CancellationToken ct)
    {
        // The cheapest call that needs valid keys with read permission.
        using var response = await SendAsync(HttpMethod.Get, "products?per_page=1&_fields=id", ct);
        using var _ = await ReadJsonAsync(response, JsonValueKind.Array, ct);
    }

    public async Task<IReadOnlyList<ExternalProduct>> FetchProductsAsync(int max, CancellationToken ct)
    {
        var products = new List<ExternalProduct>();
        void Add(ExternalProduct product)
        {
            if (products.Count >= max) throw new SalesChannelException(SalesChannelException.TooManyProducts);
            products.Add(product);
        }

        await foreach (var product in PagesAsync($"products?orderby=id&order=asc&_fields={ProductFields}", ct))
        {
            var id = Id(product);
            var name = Text(product, "name") ?? "";
            switch (Text(product, "type"))
            {
                case "simple":
                    Add(new ExternalProduct(id, null, name, null, Text(product, "sku"), Category(product), Price(product), Cost(product), Stock(product),
                        ProductImage(product)));
                    break;
                case "variable":
                    await foreach (var variation in PagesAsync($"products/{id}/variations?orderby=id&order=asc&_fields={VariationFields}", ct))
                    {
                        Add(new ExternalProduct(Id(variation), id, VariationName(name, variation), name,
                            Text(variation, "sku"), Category(product), Price(variation), Cost(variation), Stock(variation),
                            VariationImage(variation) ?? ProductImage(product)));
                    }
                    break;
                // Grouped and external products hold no stock of their own.
            }
        }

        return products;
    }

    public async Task EnsureWebhooksAsync(string deliveryUrl, string secret, CancellationToken ct)
    {
        var existing = new List<JsonElement>();
        await foreach (var webhook in PagesAsync("webhooks?_fields=id,topic,delivery_url,status", ct))
        {
            existing.Add(webhook);
        }

        foreach (var topic in WebhookTopics)
        {
            var match = existing.FirstOrDefault(w => Text(w, "topic") == topic && Text(w, "delivery_url") == deliveryUrl);
            var webhook = new { name = $"Zalihe ({topic})", topic, delivery_url = deliveryUrl, secret, status = "active" };
            // An existing one is updated too: it may be disabled after failed deliveries, or have an old secret.
            using var _ = match.ValueKind == JsonValueKind.Undefined
                ? await SendAsync(HttpMethod.Post, "webhooks", ct, webhook)
                : await SendAsync(HttpMethod.Put, $"webhooks/{Id(match)}", ct, webhook);
        }
    }

    public async Task<IReadOnlyList<ShopOrder>> FetchOrdersModifiedSinceAsync(DateTimeOffset since, CancellationToken ct)
    {
        // "any" is every status except trash. Dates are read and compared in UTC.
        var after = since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var orders = new List<ShopOrder>();
        await foreach (var order in PagesAsync(
            $"orders?modified_after={after}&dates_are_gmt=true&status=any&orderby=modified&order=asc&_fields={OrderFields}", ct))
        {
            if (WooCommerceOrderReader.Read(order) is { } read) orders.Add(read);
        }

        return orders;
    }

    public async Task<IReadOnlySet<string>> UpdateStockAsync(IReadOnlyList<ShopStockUpdate> updates, CancellationToken ct)
    {
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        // Simple products go to products/batch, variations to their own product's variations/batch.
        foreach (var group in updates.GroupBy(u => u.ParentExternalId))
        {
            var path = group.Key is null ? "products/batch" : $"products/{group.Key}/variations/batch";
            foreach (var chunk in group.Chunk(PageSize)) // WooCommerce takes at most 100 per batch
            {
                var body = new { update = chunk.Select(u => new { id = long.Parse(u.ExternalId, CultureInfo.InvariantCulture), manage_stock = true, stock_quantity = u.Stock }) };
                using var response = await SendAsync(HttpMethod.Post, path, ct, body);
                using var document = await ReadJsonAsync(response, JsonValueKind.Object, ct);

                // Each result has the product, or an "error" (e.g. a product deleted from the shop).
                if (!document.RootElement.TryGetProperty("update", out var results) || results.ValueKind != JsonValueKind.Array)
                {
                    throw new SalesChannelException(SalesChannelException.UnexpectedResponse);
                }

                foreach (var result in results.EnumerateArray())
                {
                    if (result.ValueKind == JsonValueKind.Object && !result.TryGetProperty("error", out _)) accepted.Add(Id(result));
                }
            }
        }

        return accepted;
    }

    /// <summary>Every element of a paged list, one page of <see cref="PageSize"/> at a time.</summary>
    private async IAsyncEnumerable<JsonElement> PagesAsync(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var page = 1; ; page++)
        {
            List<JsonElement> elements;
            using (var response = await SendAsync(HttpMethod.Get, $"{path}&per_page={PageSize}&page={page}", ct))
            using (var document = await ReadJsonAsync(response, JsonValueKind.Array, ct))
            {
                elements = document.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
            }

            foreach (var element in elements) yield return element;
            if (elements.Count < PageSize) yield break;
        }
    }

    private static string Id(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty("id", out var id)
        && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : throw new SalesChannelException(SalesChannelException.UnexpectedResponse);

    /// <summary>A text field, with HTML entities decoded ("Kafa &amp;amp; čaj" → "Kafa &amp; čaj"); null when empty.</summary>
    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? WebUtility.HtmlDecode(text).Trim()
            : null;

    /// <summary>The regular price, or the current price when there is none. Prices are strings ("1250.00", or "").</summary>
    private static decimal? Price(JsonElement element) =>
        Decimal(Text(element, "regular_price")) ?? Decimal(Text(element, "price"));

    /// <summary>
    /// The "Cost of goods sold" total; a variation without its own cost has its product's. Null when the
    /// shop has the feature off (no field) or no cost is set: WooCommerce reports that as 0.
    /// </summary>
    private static decimal? Cost(JsonElement element) =>
        element.TryGetProperty("cost_of_goods_sold", out var cogs) && cogs.ValueKind == JsonValueKind.Object
        && cogs.TryGetProperty("total_value", out var total) && total.ValueKind == JsonValueKind.Number
        && total.TryGetDecimal(out var value) && value > 0
            ? value
            : null;

    private static decimal? Decimal(string? text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Null when the shop doesn't track stock for the product.</summary>
    private static decimal? Stock(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty("stock_quantity", out var stock)
        && stock.ValueKind == JsonValueKind.Number && stock.TryGetDecimal(out var value)
            ? value
            : null;

    /// <summary>
    /// The product's first category. WooCommerce puts every product without a category into
    /// "Uncategorized", which says nothing, so that one is left out.
    /// </summary>
    private static string? Category(JsonElement product) =>
        product.TryGetProperty("categories", out var categories) && categories.ValueKind == JsonValueKind.Array
            ? categories.EnumerateArray()
                .Where(c => Text(c, "slug") != "uncategorized")
                .Select(c => Text(c, "name"))
                .FirstOrDefault(n => n is not null)
            : null;

    /// <summary>The address of the product's first (main) image.</summary>
    private static string? ProductImage(JsonElement product) =>
        product.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array
            ? images.EnumerateArray().Select(i => Text(i, "src")).FirstOrDefault(src => src is not null)
            : null;

    /// <summary>The variation's own image; WooCommerce usually reports the product's when it has none.</summary>
    private static string? VariationImage(JsonElement variation) =>
        variation.TryGetProperty("image", out var image) ? Text(image, "src") : null;

    /// <summary>"Majica basic – M, Siva": the product name with the variation's options.</summary>
    private static string VariationName(string productName, JsonElement variation)
    {
        var options = variation.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Array
            ? attributes.EnumerateArray().Select(a => Text(a, "option")).OfType<string>().ToList()
            : [];
        return options.Count > 0 ? $"{productName} – {string.Join(", ", options)}" : $"{productName} #{Id(variation)}";
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, CancellationToken ct, object? body = null)
    {
        using var request = new HttpRequestMessage(method, $"{baseUrl}/wp-json/wc/v3/{path}");
        if (body is not null) request.Content = JsonContent.Create(body);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.ConsumerKey}:{credentials.ConsumerSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception e) when (e is HttpRequestException || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // DNS, connection refused, TLS problems or a timeout.
            throw new SalesChannelException(SalesChannelException.Unreachable);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var code = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => SalesChannelException.Unauthorized,
            HttpStatusCode.NotFound => SalesChannelException.NotWooCommerce,
            _ => SalesChannelException.UnexpectedResponse,
        };
        response.Dispose();
        throw new SalesChannelException(code);
    }

    /// <summary>
    /// A 200 that isn't the expected JSON usually means a security plugin, a captcha page or a
    /// redirect to a login page answered instead of WooCommerce.
    /// </summary>
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, JsonValueKind expected, CancellationToken ct)
    {
        JsonDocument? document = null;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
        }

        if (document?.RootElement.ValueKind == expected) return document;
        document?.Dispose();
        throw new SalesChannelException(SalesChannelException.UnexpectedResponse);
    }
}

public class SalesChannelFactory(IHttpClientFactory httpClientFactory) : ISalesChannelFactory
{
    public const string HttpClientName = "WooCommerce";

    public ISalesChannel Create(Domain.Channels.SalesChannelType type, string baseUrl, WooCommerceCredentials credentials) =>
        type switch
        {
            Domain.Channels.SalesChannelType.WooCommerce =>
                new WooCommerceClient(httpClientFactory.CreateClient(HttpClientName), baseUrl, credentials),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
}
