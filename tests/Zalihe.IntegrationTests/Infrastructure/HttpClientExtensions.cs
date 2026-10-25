using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;

namespace Zalihe.IntegrationTests.Infrastructure;

public static class HttpClientExtensions
{
    public static async Task RegisterAsync(this HttpClient client, string email, string password = "sigurna-lozinka")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { companyName = "Test Firma", name = "Miljan", email, password });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A client signed in as the owner of a new company, sending the antiforgery header
    /// on every request, like the frontend does.
    /// </summary>
    public static async Task<HttpClient> CreateSignedInClientAsync(this ZaliheApiFactory factory)
    {
        var client = factory.CreateClient();
        await client.RegisterAsync(UniqueEmail());
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", await client.GetAntiforgeryTokenAsync());
        return client;
    }

    /// <summary>
    /// A client signed in as a user that <paramref name="owner"/> invited to its company with
    /// <paramref name="role"/> ("admin", "manager", "employee"), sending the antiforgery header like
    /// <see cref="CreateSignedInClientAsync"/>.
    /// </summary>
    public static async Task<HttpClient> CreateInvitedClientAsync(this ZaliheApiFactory factory, HttpClient owner, string role)
    {
        var email = UniqueEmail();
        (await owner.PostAsJsonAsync("/api/account/invitations", new { email, role })).EnsureSuccessStatusCode();
        var token = Regex.Match(factory.Emails.SentTo(email)[^1].Text, "/invite/([A-Za-z0-9_-]+)").Groups[1].Value;

        var client = factory.CreateClient();
        (await client.PostAsJsonAsync($"/api/invitations/{token}/accept", new { name = "Ana", password = "lozinka-pozvanog" }))
            .EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", await client.GetAntiforgeryTokenAsync());
        return client;
    }

    /// <summary>Calls "me" and returns the antiforgery token it issues in the XSRF-TOKEN cookie.</summary>
    public static async Task<string> GetAntiforgeryTokenAsync(this HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN="));
        return Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';')]);
    }

    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";
}
