using System.Net;
using System.Net.Http.Headers;
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
    public async Task CheckConnectionAsync(CancellationToken ct)
    {
        // The cheapest call that needs valid keys with read permission.
        using var response = await SendAsync(HttpMethod.Get, "products?per_page=1&_fields=id", ct);
        await EnsureJsonArrayAsync(response, ct);
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
    private static async Task EnsureJsonArrayAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (document.RootElement.ValueKind == JsonValueKind.Array) return;
        }
        catch (JsonException)
        {
        }

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
