using System.Net;
using System.Text;

namespace Zalihe.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for WooCommerce in tests: the real shop is never called. Each test sets how the
/// "shop" answers; requests are recorded so tests can check what was sent.
/// </summary>
public class FakeShopHandler : HttpMessageHandler
{
    private Func<HttpRequestMessage, HttpResponseMessage> _respond = _ => Json("[]");

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Answers every request with <paramref name="respond"/> until changed.</summary>
    public void RespondWith(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
        Requests.Clear();
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(_respond(request));
    }
}
