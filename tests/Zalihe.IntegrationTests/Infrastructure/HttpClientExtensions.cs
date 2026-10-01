using System.Net.Http.Json;
using System.Text.Json;
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
