using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;
using static Zalihe.IntegrationTests.Infrastructure.HttpClientExtensions;

namespace Zalihe.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public partial class PasswordResetTests(ZaliheApiFactory factory)
{
    private const string NewPassword = "nova-sigurna-lozinka";

    [GeneratedRegex(@"/reset-password\?email=[^&\s]+&token=([A-Za-z0-9_-]+)")]
    private static partial Regex ResetLink();

    /// <summary>Registers a fresh account (password "sigurna-lozinka") and signs nobody in.</summary>
    private async Task<string> AccountAsync()
    {
        var email = UniqueEmail();
        await factory.CreateClient().RegisterAsync(email);
        return email;
    }

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email });

    /// <summary>Asks for a reset link and returns the token from the emailed link.</summary>
    private async Task<string> TokenAsync(string email)
    {
        (await ForgotAsync(email)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return ResetLink().Match(factory.Emails.SentTo(email)[^1].Text).Groups[1].Value;
    }

    private Task<HttpResponseMessage> ResetAsync(string email, string token, HttpClient? client = null, string password = NewPassword) =>
        (client ?? factory.CreateClient()).PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword = password });

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

    private static async Task<JsonElement> FirstErrorAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        return (await response.ReadProblemAsync()).GetProperty("errors")[0];
    }

    [Fact]
    public async Task ForgotPassword_KnownAddress_EmailsALinkInTheUsersLanguage()
    {
        // Arrange
        var email = await AccountAsync();

        // Act
        var response = await ForgotAsync($"  {email.ToUpperInvariant()} ");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var sent = factory.Emails.SentTo(email).ShouldHaveSingleItem();
        sent.Subject.ShouldBe("Nova lozinka za Lagerko");
        sent.Html.ShouldContain($"https://app.zalihe.test/reset-password?email={Uri.EscapeDataString(email)}&amp;token=");
        ResetLink().IsMatch(sent.Text).ShouldBeTrue();
    }

    [Fact]
    public async Task ForgotPassword_UnknownAddress_Returns204AndSendsNothing()
    {
        // Arrange
        var email = UniqueEmail();

        // Act
        var response = await ForgotAsync(email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task ForgotPassword_AgainRightAway_SendsOnlyOneEmail()
    {
        // Arrange
        var email = await AccountAsync();
        await ForgotAsync(email);

        // Act
        var response = await ForgotAsync(email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        factory.Emails.SentTo(email).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ResetPassword_ValidLink_SetsTheNewPasswordAndSignsIn()
    {
        // Arrange
        var email = await AccountAsync();
        var token = await TokenAsync(email);
        var client = factory.CreateClient();

        // Act
        var response = await ResetAsync(email, token, client);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("email").GetString().ShouldBe(email);
        (await LoginAsync(email, "sigurna-lozinka")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_LinkUsedTwice_Returns400ResetInvalid()
    {
        // Arrange
        var email = await AccountAsync();
        var token = await TokenAsync(email);
        await ResetAsync(email, token);

        // Act
        var error = await FirstErrorAsync(await ResetAsync(email, token, password: "jos-jedna-lozinka"));

        // Assert
        error.GetProperty("code").GetString().ShouldBe("auth.reset_invalid");
        (await LoginAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("nije-token")]
    [InlineData("%%%")]
    public async Task ResetPassword_WrongToken_Returns400ResetInvalid(string token)
    {
        // Arrange
        var email = await AccountAsync();

        // Act
        var error = await FirstErrorAsync(await ResetAsync(email, token));

        // Assert
        error.GetProperty("code").GetString().ShouldBe("auth.reset_invalid");
        (await LoginAsync(email, "sigurna-lozinka")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_TokenOfAnotherAccount_Returns400ResetInvalid()
    {
        // Arrange
        var victim = await AccountAsync();
        var attacker = await AccountAsync();
        var attackersToken = await TokenAsync(attacker);

        // Act
        var error = await FirstErrorAsync(await ResetAsync(victim, attackersToken));

        // Assert
        error.GetProperty("code").GetString().ShouldBe("auth.reset_invalid");
        (await LoginAsync(victim, "sigurna-lozinka")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_NewTooShort_Returns400OnTheNewField()
    {
        // Arrange
        var email = await AccountAsync();
        var token = await TokenAsync(email);

        // Act
        var error = await FirstErrorAsync(await ResetAsync(email, token, password: "kratka"));

        // Assert
        error.GetProperty("code").GetString().ShouldBe("auth.password_too_short");
        error.GetProperty("field").GetString().ShouldBe("newPassword");
    }

    [Fact]
    public async Task ResetPassword_LockedOutAccount_UnlocksIt()
    {
        // Arrange
        var email = await AccountAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(email, "pogresna-lozinka");
        }

        var locked = await (await LoginAsync(email, "sigurna-lozinka")).ReadProblemAsync();
        locked.GetProperty("code").GetString().ShouldBe("auth.locked_out");
        var token = await TokenAsync(email);

        // Act
        var response = await ResetAsync(email, token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
