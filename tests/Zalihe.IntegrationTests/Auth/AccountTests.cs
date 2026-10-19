using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class AccountTests(ZaliheApiFactory factory)
{
    private const string Password = "sigurna-lozinka";

    /// <summary>A signed-in client sending the antiforgery header, with its email.</summary>
    private async Task<(HttpClient Client, string Email)> SignedInAsync()
    {
        var client = factory.CreateClient();
        var email = HttpClientExtensions.UniqueEmail();
        await client.RegisterAsync(email, Password);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", await client.GetAntiforgeryTokenAsync());
        return (client, email);
    }

    private static async Task<JsonElement> MeAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

    private static async Task<JsonElement> FirstErrorAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        return (await response.ReadProblemAsync()).GetProperty("errors")[0];
    }

    [Fact]
    public async Task UpdateCompany_NewName_IsTrimmedAndShownToTheUser()
    {
        // Arrange
        var (client, _) = await SignedInAsync();

        // Act
        var response = await client.PutAsJsonAsync("/api/account/company", new { name = "  Zrno d.o.o.  " });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantName").GetString().ShouldBe("Zrno d.o.o.");
        (await MeAsync(client)).GetProperty("tenantName").GetString().ShouldBe("Zrno d.o.o.");
    }

    [Fact]
    public async Task UpdateCompany_OneCompany_DoesNotRenameAnother()
    {
        // Arrange
        var (client, _) = await SignedInAsync();
        var (other, _) = await SignedInAsync();

        // Act
        await client.PutAsJsonAsync("/api/account/company", new { name = "Zrno d.o.o." });

        // Assert
        (await MeAsync(other)).GetProperty("tenantName").GetString().ShouldBe("Test Firma");
    }

    [Theory]
    [InlineData("", "validation.required")]
    [InlineData("   ", "validation.required")]
    public async Task UpdateCompany_EmptyName_Returns400AndKeepsTheName(string name, string code)
    {
        // Arrange
        var (client, _) = await SignedInAsync();

        // Act
        var error = await FirstErrorAsync(await client.PutAsJsonAsync("/api/account/company", new { name }));

        // Assert
        error.GetProperty("code").GetString().ShouldBe(code);
        error.GetProperty("field").GetString().ShouldBe("name");
        (await MeAsync(client)).GetProperty("tenantName").GetString().ShouldBe("Test Firma");
    }

    [Fact]
    public async Task UpdateCompany_NameTooLong_Returns400()
    {
        var (client, _) = await SignedInAsync();

        var error = await FirstErrorAsync(await client.PutAsJsonAsync("/api/account/company", new { name = new string('a', 201) }));

        error.GetProperty("code").GetString().ShouldBe("validation.max_length");
    }

    [Fact]
    public async Task UpdateProfile_NameAndLanguage_AreSavedOnTheUser()
    {
        // Arrange
        var (client, _) = await SignedInAsync();

        // Act
        var response = await client.PutAsJsonAsync("/api/account/profile", new { name = " Ana ", language = "en" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await MeAsync(client);
        me.GetProperty("name").GetString().ShouldBe("Ana");
        me.GetProperty("language").GetString().ShouldBe("en");
    }

    [Fact]
    public async Task UpdateProfile_UnsupportedLanguage_Returns400()
    {
        var (client, _) = await SignedInAsync();

        var error = await FirstErrorAsync(await client.PutAsJsonAsync("/api/account/profile", new { name = "Ana", language = "de" }));

        error.GetProperty("code").GetString().ShouldBe("validation.unsupported_language");
        error.GetProperty("field").GetString().ShouldBe("language");
        (await MeAsync(client)).GetProperty("language").GetString().ShouldBe("sr-Latn");
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrent_StaysSignedInAndOnlyTheNewOneWorks()
    {
        // Arrange
        var (client, email) = await SignedInAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/account/password",
            new { currentPassword = Password, newPassword = "nova-sigurna-lozinka" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = factory.CreateClient();
        (await login.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await login.PostAsJsonAsync("/api/auth/login", new { email, password = "nova-sigurna-lozinka" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400OnTheCurrentField()
    {
        var (client, _) = await SignedInAsync();

        var error = await FirstErrorAsync(await client.PostAsJsonAsync("/api/account/password",
            new { currentPassword = "pogresna-lozinka", newPassword = "nova-sigurna-lozinka" }));

        error.GetProperty("code").GetString().ShouldBe("auth.wrong_password");
        error.GetProperty("field").GetString().ShouldBe("currentPassword");
    }

    [Fact]
    public async Task ChangePassword_NewTooShort_Returns400OnTheNewField()
    {
        // Arrange
        var (client, email) = await SignedInAsync();

        // Act
        var error = await FirstErrorAsync(await client.PostAsJsonAsync("/api/account/password",
            new { currentPassword = Password, newPassword = "kratka" }));

        // Assert
        error.GetProperty("code").GetString().ShouldBe("auth.password_too_short");
        error.GetProperty("field").GetString().ShouldBe("newPassword");
        error.GetProperty("params").GetProperty("min").GetInt32().ShouldBe(8);
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("PUT", "/api/account/company")]
    [InlineData("PUT", "/api/account/profile")]
    [InlineData("POST", "/api/account/password")]
    public async Task Account_Anonymous_Returns401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { name = "x" }) };

        (await factory.CreateClient().SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCompany_CookieWithoutAntiforgeryToken_IsRefused()
    {
        // Arrange
        var (client, _) = await SignedInAsync();
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        // Act
        var response = await client.PutAsJsonAsync("/api/account/company", new { name = "Tuđa firma" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.antiforgery_invalid");
        (await MeAsync(client)).GetProperty("tenantName").GetString().ShouldBe("Test Firma");
    }
}
