using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;

namespace Zalihe.IntegrationTests.Infrastructure;

public static class HttpClientExtensions
{
    /// <summary>Registers a company all the way, with the code from the email, which signs the client in.</summary>
    /// <returns>The answer to the confirmation, which carries the sign-in cookie.</returns>
    public static async Task<HttpResponseMessage> RegisterAsync(this HttpClient client, ZaliheApiFactory factory, string email,
        string password = "sigurna-lozinka", string companyName = "Test Firma", string name = "Miljan", string? language = null)
    {
        var start = await client.StartRegistrationAsync(email, password, companyName, name, language);
        start.EnsureSuccessStatusCode();
        var confirm = await client.ConfirmRegistrationAsync(await start.ReadTokenAsync(), factory.Emails.LatestCode(email));
        confirm.EnsureSuccessStatusCode();
        return confirm;
    }

    public static Task<HttpResponseMessage> StartRegistrationAsync(this HttpClient client, string email,
        string password = "sigurna-lozinka", string companyName = "Test Firma", string name = "Miljan", string? language = null) =>
        client.PostAsJsonAsync("/api/auth/register", new { companyName, name, email, password, language });

    public static Task<HttpResponseMessage> ConfirmRegistrationAsync(this HttpClient client, string token, string code) =>
        client.PostAsJsonAsync("/api/auth/register/confirm", new { token, code });

    public static async Task<string> ReadTokenAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

    /// <summary>
    /// A client signed in as the owner of a new company, sending the antiforgery header
    /// on every request, like the frontend does.
    /// </summary>
    public static async Task<HttpClient> CreateSignedInClientAsync(this ZaliheApiFactory factory)
    {
        var client = factory.CreateClient();
        await client.RegisterAsync(factory, UniqueEmail());
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

    /// <summary>The sign-in cookie a response sets, with its attributes.</summary>
    public static string SignInCookie(this HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("zalihe.auth="));

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
