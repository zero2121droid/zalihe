using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Zalihe.Infrastructure.Email;
using Zalihe.IntegrationTests.Infrastructure;
using static Zalihe.IntegrationTests.Infrastructure.HttpClientExtensions;

namespace Zalihe.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class NewRegistrationEmailTests(ZaliheApiFactory factory)
{
    private const string Owner = ZaliheApiFactory.PlatformOwnerEmail;

    /// <summary>The email about <paramref name="email"/>'s company, which a background job sends a moment after the registration.</summary>
    private async Task<EmailMessage?> OwnerEmailAboutAsync(string email)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (factory.Emails.SentTo(Owner).FirstOrDefault(m => m.Text.Contains(email)) is { } sent) return sent;
            await Task.Delay(100);
        }

        return null;
    }

    [Fact]
    public async Task Register_NewCompany_EmailsThePlatformOwner()
    {
        // Arrange
        var email = UniqueEmail();

        // Act
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { companyName = "Pržionica Sever", name = "Ana", email, password = "sigurna-lozinka", language = "en" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var sent = (await OwnerEmailAboutAsync(email)).ShouldNotBeNull();
        sent.Subject.ShouldBe("Nova firma u Lagerku: Pržionica Sever");
        sent.Text.ShouldContain($"Firma Pržionica Sever je upravo registrovana. Vlasnik: Ana, {email}.");
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Register_MailServerDown_StillSucceeds()
    {
        // Arrange
        var email = UniqueEmail();
        factory.Emails.Fail = m => m.To == Owner && m.Text.Contains(email) ? new IOException("SMTP unreachable") : null;

        try
        {
            // Act
            await factory.CreateClient().RegisterAsync(email);

            // Assert: signing in works, so the company was saved although no email went out
            var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "sigurna-lozinka" });
            login.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        finally
        {
            factory.Emails.Fail = _ => null;
        }
    }

    [Fact]
    public async Task AcceptInvitation_ExistingCompany_DoesNotEmailThePlatformOwner()
    {
        // Arrange: a name no other test uses, because their jobs may still be sending
        var email = UniqueEmail();
        var company = $"Firma {Guid.NewGuid():N}";
        var owner = factory.CreateClient();
        (await owner.PostAsJsonAsync("/api/auth/register",
            new { companyName = company, name = "Ana", email, password = "sigurna-lozinka" })).EnsureSuccessStatusCode();
        owner.DefaultRequestHeaders.Add("X-XSRF-TOKEN", await owner.GetAntiforgeryTokenAsync());
        (await OwnerEmailAboutAsync(email)).ShouldNotBeNull();

        // Act
        await factory.CreateInvitedClientAsync(owner, "employee");
        await Task.Delay(500);

        // Assert
        factory.Emails.SentTo(Owner).Count(m => m.Text.Contains(company)).ShouldBe(1);
    }
}
