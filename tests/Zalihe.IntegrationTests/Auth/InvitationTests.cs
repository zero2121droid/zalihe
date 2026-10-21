using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public partial class InvitationTests(ZaliheApiFactory factory)
{
    [GeneratedRegex(@"/invite/([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/account/invitations", new { email });

    /// <summary>Invites a fresh address and returns it with the token from the emailed link.</summary>
    private async Task<(string Email, string Token)> InvitedAsync(HttpClient inviter)
    {
        var email = HttpClientExtensions.UniqueEmail();
        (await InviteAsync(inviter, email)).StatusCode.ShouldBe(HttpStatusCode.OK);
        return (email, TokenFrom(factory.Emails.SentTo(email)[^1].Text));
    }

    private static string TokenFrom(string text) => InviteLink().Match(text).Groups[1].Value;

    private Task<HttpResponseMessage> AcceptAsync(string token, HttpClient? client = null, string password = "lozinka-pozvanog") =>
        (client ?? factory.CreateClient()).PostAsJsonAsync($"/api/invitations/{token}/accept", new { name = "Ana", password });

    [Fact]
    public async Task Invite_NewAddress_EmailsALinkInTheInvitersLanguage()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var email = HttpClientExtensions.UniqueEmail();

        // Act
        var response = await InviteAsync(client, $"  {email.ToUpperInvariant()} ");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var invitation = await response.Content.ReadFromJsonAsync<JsonElement>();
        invitation.GetProperty("email").GetString().ShouldBe(email);
        invitation.GetProperty("state").GetString().ShouldBe("pending");
        var sent = factory.Emails.SentTo(email).ShouldHaveSingleItem();
        sent.Subject.ShouldBe("Miljan te poziva u Lagerko");
        sent.Html.ShouldContain("https://app.zalihe.test/invite/");
        TokenFrom(sent.Text).Length.ShouldBe(43);
    }

    [Fact]
    public async Task Accept_ValidLink_CreatesTheUserInTheSameCompanyAndSignsIn()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        await owner.PostAsJsonAsync("/api/items", new { name = "Kafa Etiopija", sku = "KF-ETI", unit = "kom", minStock = 0 });
        var (email, token) = await InvitedAsync(owner);
        var invited = factory.CreateClient();

        // Act
        var info = await invited.GetFromJsonAsync<JsonElement>($"/api/invitations/{token}");
        var response = await AcceptAsync(token, invited);

        // Assert
        info.GetProperty("companyName").GetString().ShouldBe("Test Firma");
        info.GetProperty("email").GetString().ShouldBe(email);
        info.GetProperty("invitedBy").GetString().ShouldBe("Miljan");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = await invited.GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("email").GetString().ShouldBe(email);
        me.GetProperty("name").GetString().ShouldBe("Ana");
        var ownerMe = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("tenantId").GetString().ShouldBe(ownerMe.GetProperty("tenantId").GetString());
        var items = await invited.GetFromJsonAsync<JsonElement>("/api/items");
        items.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()).ShouldBe(["KF-ETI"]);
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "lozinka-pozvanog" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Accept_SameLinkTwice_SecondTimeIsInvalid()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var (_, token) = await InvitedAsync(owner);
        await AcceptAsync(token);

        // Act
        var again = await AcceptAsync(token);

        // Assert
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await again.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("invitation.invalid");
        (await factory.CreateClient().GetAsync($"/api/invitations/{token}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invite_SameAddressAgain_OldLinkStopsWorkingAndNewOneWorks()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var (email, oldToken) = await InvitedAsync(owner);

        // Act
        await InviteAsync(owner, email);
        var newToken = TokenFrom(factory.Emails.SentTo(email)[^1].Text);

        // Assert
        newToken.ShouldNotBe(oldToken);
        (await factory.CreateClient().GetAsync($"/api/invitations/{oldToken}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AcceptAsync(newToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var pending = await owner.GetFromJsonAsync<JsonElement>("/api/account/invitations");
        pending.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Revoke_PendingInvitation_LinkStopsWorking()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var (_, token) = await InvitedAsync(owner);
        var id = (await owner.GetFromJsonAsync<JsonElement>("/api/account/invitations")).GetProperty("items")[0].GetProperty("id").GetString();

        // Act
        var response = await owner.DeleteAsync($"/api/account/invitations/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AcceptAsync(token)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await owner.GetFromJsonAsync<JsonElement>("/api/account/invitations")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Accept_AfterAWeek_IsInvalid()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var (email, token) = await InvitedAsync(owner);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
                $"""UPDATE "Invitations" SET "ExpiresAt" = now() - interval '1 minute' WHERE "Email" = {email}""");
        }

        // Act
        var response = await AcceptAsync(token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var listed = (await owner.GetFromJsonAsync<JsonElement>("/api/account/invitations")).GetProperty("items")[0];
        listed.GetProperty("state").GetString().ShouldBe("expired");
    }

    [Fact]
    public async Task Invite_AddressWithAnAccount_Returns400()
    {
        // Arrange: someone already registered, in this company or another.
        var owner = await factory.CreateSignedInClientAsync();
        var existing = HttpClientExtensions.UniqueEmail();
        await factory.CreateClient().RegisterAsync(existing);

        // Act
        var response = await InviteAsync(owner, existing);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("invitation.email_taken");
        error.GetProperty("field").GetString().ShouldBe("email");
        factory.Emails.SentTo(existing).ShouldBeEmpty();
    }

    [Fact]
    public async Task Invite_NotAnEmail_Returns400()
    {
        var owner = await factory.CreateSignedInClientAsync();

        var response = await InviteAsync(owner, "nije-email");

        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("validation.email");
    }

    [Fact]
    public async Task Invite_MailServerDown_Returns503AndSavesNothing()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var email = HttpClientExtensions.UniqueEmail();
        factory.Emails.Fail = m => m.To == email ? new IOException("SMTP unreachable") : null;

        // Act
        HttpResponseMessage response;
        try
        {
            response = await InviteAsync(owner, email);
        }
        finally
        {
            factory.Emails.Fail = _ => null;
        }

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("email.unavailable");
        (await owner.GetFromJsonAsync<JsonElement>("/api/account/invitations")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Accept_PasswordTooShort_Returns400AndKeepsTheInvitation()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var (_, token) = await InvitedAsync(owner);

        // Act
        var response = await AcceptAsync(token, password: "kratka");

        // Assert
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("auth.password_too_short");
        (await factory.CreateClient().GetAsync($"/api/invitations/{token}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UsersAndInvitations_OfAnotherCompany_AreNotVisibleOrRevocable()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var (_, token) = await InvitedAsync(owner);
        await AcceptAsync(token);
        await InvitedAsync(owner);
        var id = (await owner.GetFromJsonAsync<JsonElement>("/api/account/invitations")).GetProperty("items")[0].GetProperty("id").GetString();
        var other = await factory.CreateSignedInClientAsync();

        // Act
        var otherUsers = await other.GetFromJsonAsync<JsonElement>("/api/account/users");
        var otherInvitations = await other.GetFromJsonAsync<JsonElement>("/api/account/invitations");
        var revoke = await other.DeleteAsync($"/api/account/invitations/{id}");

        // Assert
        otherUsers.GetProperty("totalCount").GetInt32().ShouldBe(1);
        otherInvitations.GetProperty("totalCount").GetInt32().ShouldBe(0);
        revoke.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await owner.GetFromJsonAsync<JsonElement>("/api/account/users")).GetProperty("totalCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task GetInvitation_UnknownToken_Returns404()
    {
        var response = await factory.CreateClient().GetAsync("/api/invitations/nepostojeci-token");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("invitation.invalid");
    }

    [Fact]
    public async Task Invite_Anonymous_Returns401()
    {
        (await InviteAsync(factory.CreateClient(), HttpClientExtensions.UniqueEmail())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
