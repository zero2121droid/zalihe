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

    /// <summary>The body of each request in <see cref="Requests"/> (null without one), read when it was sent.</summary>
    public List<string?> Bodies { get; } = [];

    /// <summary>Answers every request with <paramref name="respond"/> until changed.</summary>
    public void RespondWith(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
        Requests.Clear();
        Bodies.Clear();
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // The client disposes the request after sending, so its body can't be read later.
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
        Requests.Add(request);
        return _respond(request);
    }
}
