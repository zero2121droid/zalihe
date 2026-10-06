using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
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
    private const string ProductFields = "id,name,type,sku,regular_price,price,stock_quantity,categories";
    private const string VariationFields = "id,sku,regular_price,price,stock_quantity,attributes";

    public async Task CheckConnectionAsync(CancellationToken ct)
    {
        // The cheapest call that needs valid keys with read permission.
        using var response = await SendAsync(HttpMethod.Get, "products?per_page=1&_fields=id", ct);
        using var _ = await ReadJsonArrayAsync(response, ct);
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
                    Add(new ExternalProduct(id, null, name, null, Text(product, "sku"), Category(product), Price(product), Stock(product)));
                    break;
                case "variable":
                    await foreach (var variation in PagesAsync($"products/{id}/variations?orderby=id&order=asc&_fields={VariationFields}", ct))
                    {
                        Add(new ExternalProduct(Id(variation), id, VariationName(name, variation), name,
                            Text(variation, "sku"), Category(product), Price(variation), Stock(variation)));
                    }
                    break;
                // Grouped and external products hold no stock of their own.
            }
        }

        return products;
    }

    /// <summary>Every element of a paged list, one page of <see cref="PageSize"/> at a time.</summary>
    private async IAsyncEnumerable<JsonElement> PagesAsync(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var page = 1; ; page++)
        {
            List<JsonElement> elements;
            using (var response = await SendAsync(HttpMethod.Get, $"{path}&per_page={PageSize}&page={page}", ct))
            using (var document = await ReadJsonArrayAsync(response, ct))
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

    /// <summary>"Majica basic – M, Siva": the product name with the variation's options.</summary>
    private static string VariationName(string productName, JsonElement variation)
    {
        var options = variation.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Array
            ? attributes.EnumerateArray().Select(a => Text(a, "option")).OfType<string>().ToList()
            : [];
        return options.Count > 0 ? $"{productName} – {string.Join(", ", options)}" : $"{productName} #{Id(variation)}";
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{baseUrl}/wp-json/wc/v3/{path}");
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
    private static async Task<JsonDocument> ReadJsonArrayAsync(HttpResponseMessage response, CancellationToken ct)
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

        if (document?.RootElement.ValueKind == JsonValueKind.Array) return document;
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
