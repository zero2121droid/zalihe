using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Hosting;

/// <summary>How the app behaves as deployed: React served from the API, behind an HTTPS proxy.</summary>
[Collection(ApiCollection.Name)]
public class HostingTests(ZaliheApiFactory factory)
{
    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/register",
            new { companyName = "Test Firma", name = "Miljan", email = HttpClientExtensions.UniqueEmail(), password = "sigurna-lozinka" });

    private static string Cookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(name.EndsWith('.') ? name : name + "="));

    [Theory]
    [InlineData("/")]
    [InlineData("/items/0199f000-0000-7000-8000-000000000000")]
    [InlineData("/invite/abc_DEF-123")]
    public async Task Get_ReactRoute_ReturnsIndexHtmlThatIsNotCached(string path)
    {
        // Act
        var response = await factory.CreateClient().GetAsync(path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        response.Headers.CacheControl?.NoCache.ShouldBeTrue();
        (await response.Content.ReadAsStringAsync()).ShouldBe(ZaliheApiFactory.IndexHtml);
    }

    [Fact]
    public async Task Get_HashedAsset_IsCachedForAYear()
    {
        var response = await factory.CreateClient().GetAsync(ZaliheApiFactory.AssetPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl?.MaxAge.ShouldBe(TimeSpan.FromDays(365));
        response.Headers.CacheControl?.Public.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/api/nepostojece")]
    [InlineData("/api/items/0199f000-0000-7000-8000-000000000000/nepostojece")]
    public async Task Get_UnknownApiAddress_Returns404ProblemNotThePage(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("common.not_found");
    }

    [Fact]
    public async Task Get_MissingFile_Returns404()
    {
        (await factory.CreateClient().GetAsync("/nepostojece.png")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_BehindHttpsProxy_CookiesAreSecure()
    {
        // Arrange: Caddy ends HTTPS and forwards plain HTTP with the original scheme. The test client
        // talks plain HTTP, so it would rightly hold back a Secure cookie; it is sent by hand instead.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        // Act
        var register = await RegisterAsync(client);
        var auth = Cookie(register, "zalihe.auth");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", auth[..auth.IndexOf(';')]);
        var me = await client.SendAsync(request);

        // Assert
        register.EnsureSuccessStatusCode();
        auth.ShouldContain("secure", Case.Insensitive);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        Cookie(me, "XSRF-TOKEN").ShouldContain("secure", Case.Insensitive);
        Cookie(me, ".AspNetCore.Antiforgery.").ShouldContain("secure", Case.Insensitive);
    }

    [Fact]
    public async Task Register_OverPlainHttp_AuthCookieIsNotSecure()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await RegisterAsync(client);

        // Assert
        response.EnsureSuccessStatusCode();
        Cookie(response, "zalihe.auth").ShouldNotContain("secure", Case.Insensitive);
    }

    [Fact]
    public async Task Health_DatabaseUp_Returns200()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }
}
