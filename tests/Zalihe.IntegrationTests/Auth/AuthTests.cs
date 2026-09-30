using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;
using static Zalihe.IntegrationTests.Infrastructure.HttpClientExtensions;

namespace Zalihe.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class AuthTests(ZaliheApiFactory factory)
{
    [Fact]
    public async Task Register_ValidData_SignsInOwnerOfNewCompany()
    {
        // Arrange
        var client = factory.CreateClient();
        var email = UniqueEmail();

        // Act
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { companyName = "  Prodavnica Zalihe  ", email, password = "sigurna-lozinka", language = "en" });
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        // Assert
        register.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        me.GetProperty("email").GetString().ShouldBe(email);
        me.GetProperty("language").GetString().ShouldBe("en");
        me.GetProperty("tenantName").GetString().ShouldBe("Prodavnica Zalihe");
    }

    [Fact]
    public async Task Register_NoLanguage_UsesSerbianLatin()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        await client.RegisterAsync(UniqueEmail());
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        // Assert
        me.GetProperty("language").GetString().ShouldBe("sr-Latn");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsEmailTakenCode()
    {
        // Arrange
        var email = UniqueEmail();
        await factory.CreateClient().RegisterAsync(email);

        // Act
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { companyName = "Druga Firma", email, password = "sigurna-lozinka" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        var error = problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("auth.email_taken");
        error.GetProperty("field").GetString().ShouldBe("email");
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsPasswordTooShortWithMinimum()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { companyName = "Firma", email = UniqueEmail(), password = "kratka" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("auth.password_too_short");
        error.GetProperty("params").GetProperty("min").GetInt32().ShouldBe(8);
    }

    [Fact]
    public async Task Register_MissingFields_ReturnsValidationCodesPerField()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { companyName = " ", email = "nije-email", password = "sigurna-lozinka" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray()
            .Select(e => (Field: e.GetProperty("field").GetString(), Code: e.GetProperty("code").GetString()))
            .ToList();
        errors.ShouldBe([("companyName", "validation.required"), ("email", "validation.email")], ignoreOrder: true);
    }

    [Fact]
    public async Task Register_UnsupportedLanguage_ReturnsUnsupportedLanguageCode()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { companyName = "Firma", email = UniqueEmail(), password = "sigurna-lozinka", language = "de" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("validation.unsupported_language");
    }

    [Fact]
    public async Task Login_CorrectPassword_SignsIn()
    {
        // Arrange
        var email = UniqueEmail();
        await factory.CreateClient().RegisterAsync(email);
        var client = factory.CreateClient();

        // Act
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "sigurna-lozinka" });
        var me = await client.GetAsync("/api/auth/me");

        // Assert
        login.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsInvalidCredentialsCode()
    {
        // Arrange
        var email = UniqueEmail();
        await factory.CreateClient().RegisterAsync(email);

        // Act
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "pogresna-lozinka" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Me_Anonymous_Returns401ProblemInsteadOfRedirect()
    {
        // Arrange
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        // Act
        var response = await client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.unauthenticated");
    }

    [Fact]
    public async Task Logout_CookieWithoutAntiforgeryToken_ReturnsAntiforgeryInvalid()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.RegisterAsync(UniqueEmail());

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.antiforgery_invalid");
        (await client.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_WithAntiforgeryToken_SignsOut()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.RegisterAsync(UniqueEmail());
        var token = await client.GetAntiforgeryTokenAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", token);
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
