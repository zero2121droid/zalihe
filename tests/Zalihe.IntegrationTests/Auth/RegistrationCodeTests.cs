using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Identity;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;
using static Zalihe.IntegrationTests.Infrastructure.HttpClientExtensions;

namespace Zalihe.IntegrationTests.Auth;

/// <summary>Confirming the address with an emailed code before a company exists (SPEC 2.7).</summary>
[Collection(ApiCollection.Name)]
public class RegistrationCodeTests(ZaliheApiFactory factory)
{
    private const string Password = "sigurna-lozinka";

    private static async Task<JsonElement> ErrorAsync(HttpResponseMessage response) =>
        (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();

    private async Task<string> StartAsync(string email)
    {
        var start = await factory.CreateClient().StartRegistrationAsync(email);
        start.EnsureSuccessStatusCode();
        return await start.ReadTokenAsync();
    }

    private Task<HttpResponseMessage> ResendAsync(string token) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/register/resend", new { token });

    private async Task<HttpResponseMessage> LoginAsync(string email, string password = Password) =>
        await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

    /// <summary>Moves the last code back in time, as if the wait for a new one were over.</summary>
    private async Task AgeCodeAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.PendingRegistrations.Where(p => p.Email == email).ExecuteUpdateAsync(s =>
            s.SetProperty(p => p.CodeSentAt, p => p.CodeSentAt - PendingRegistration.ResendAfter));
    }

    private async Task<int> CountPendingAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PendingRegistrations.CountAsync(p => p.Email == email);
    }

    [Fact]
    public async Task Register_ValidData_EmailsTheCodeButCreatesNoAccountYet()
    {
        // Arrange
        var email = UniqueEmail();
        var client = factory.CreateClient();

        // Act
        var response = await client.StartRegistrationAsync(email, companyName: "Kupi kripto na zlo.example", language: "en");
        var me = await client.GetAsync("/api/auth/me");
        var login = await LoginAsync(email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var sent = factory.Emails.CodesTo(email).ShouldHaveSingleItem();
        sent.Subject.ShouldBe($"{factory.Emails.LatestCode(email)} is your Lagerko code");
        sent.Text.ShouldContain(factory.Emails.LatestCode(email));
        sent.Text.ShouldNotContain("zlo.example");
        sent.Html.ShouldNotContain("zlo.example");
    }

    [Fact]
    public async Task Confirm_CorrectCode_RemovesTheWaitingRegistrationAndTheCompanyCanSignIn()
    {
        // Arrange
        var email = UniqueEmail();
        var token = await StartAsync(email);

        // Act
        var response = await factory.CreateClient().ConfirmRegistrationAsync(token, factory.Emails.LatestCode(email));
        var again = await factory.CreateClient().ConfirmRegistrationAsync(token, factory.Emails.LatestCode(email));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginAsync(email)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountPendingAsync(email)).ShouldBe(0);
        (await ErrorAsync(again)).GetProperty("code").GetString().ShouldBe(AccountService.RegistrationExpired);
    }

    [Fact]
    public async Task Confirm_WrongCode_ReturnsErrorOnTheCodeField()
    {
        // Arrange
        var email = UniqueEmail();
        var token = await StartAsync(email);

        // Act
        var response = await factory.CreateClient().ConfirmRegistrationAsync(token, "abcdef");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ErrorAsync(response);
        error.GetProperty("code").GetString().ShouldBe(AccountService.CodeInvalid);
        error.GetProperty("field").GetString().ShouldBe("code");
    }

    [Fact]
    public async Task Confirm_GuessesAtTheSameTime_OnlyTheAllowedNumberIsChecked()
    {
        // Arrange
        var email = UniqueEmail();
        var token = await StartAsync(email);
        var client = factory.CreateClient();

        // Act
        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => client.ConfirmRegistrationAsync(token, "abcdef")));
        var correct = await client.ConfirmRegistrationAsync(token, factory.Emails.LatestCode(email));

        // Assert
        var codes = await Task.WhenAll(responses.Select(async r => (await ErrorAsync(r)).GetProperty("code").GetString()));
        codes.Count(c => c == AccountService.CodeInvalid).ShouldBe(PendingRegistration.MaxAttempts - 1);
        (await ErrorAsync(correct)).GetProperty("code").GetString().ShouldBe(AccountService.CodeAttemptsExceeded);
    }

    [Fact]
    public async Task Confirm_SomeoneElseRegisteredTheSameAddressMeanwhile_TheOldScreenCanNotFinish()
    {
        // Arrange: the real owner is on the code screen, then a stranger registers the same address
        var email = UniqueEmail();
        var ownerToken = await StartAsync(email);
        var stranger = await factory.CreateClient().StartRegistrationAsync(email, "lozinka-stranca");
        stranger.EnsureSuccessStatusCode();

        // Act: the owner types the newest code from the inbox
        var response = await factory.CreateClient().ConfirmRegistrationAsync(ownerToken, factory.Emails.LatestCode(email));

        // Assert
        (await ErrorAsync(response)).GetProperty("code").GetString().ShouldBe(AccountService.RegistrationExpired);
        (await LoginAsync(email, "lozinka-stranca")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_SameAddressAgain_TheNewPasswordIsTheOneThatCounts()
    {
        // Arrange
        var email = UniqueEmail();
        await StartAsync(email);

        // Act
        await factory.CreateClient().RegisterAsync(factory, email, "ispravljena-lozinka");

        // Assert
        (await LoginAsync(email, "ispravljena-lozinka")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountPendingAsync(email)).ShouldBe(0);
    }

    [Fact]
    public async Task Register_LimitOfCodesReached_SendsNoMoreEmails()
    {
        // Arrange
        var email = UniqueEmail();
        for (var sent = 0; sent < PendingRegistration.MaxCodes; sent++)
        {
            await StartAsync(email);
        }

        // Act
        var response = await factory.CreateClient().StartRegistrationAsync(email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorAsync(response)).GetProperty("code").GetString().ShouldBe(AccountService.CodeLimitReached);
        factory.Emails.CodesTo(email).Count.ShouldBe(PendingRegistration.MaxCodes);
    }

    [Fact]
    public async Task Resend_TooSoon_SendsNothing()
    {
        // Arrange
        var email = UniqueEmail();
        var token = await StartAsync(email);

        // Act
        var response = await ResendAsync(token);

        // Assert
        (await ErrorAsync(response)).GetProperty("code").GetString().ShouldBe(AccountService.CodeResendTooSoon);
        factory.Emails.CodesTo(email).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Resend_AfterWaiting_OnlyTheNewCodeWorks()
    {
        // Arrange
        var email = UniqueEmail();
        var token = await StartAsync(email);
        var oldCode = factory.Emails.LatestCode(email);
        await AgeCodeAsync(email);

        // Act
        var response = await ResendAsync(token);
        var withOld = await factory.CreateClient().ConfirmRegistrationAsync(token, oldCode);
        var withNew = await factory.CreateClient().ConfirmRegistrationAsync(token, factory.Emails.LatestCode(email));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        factory.Emails.CodesTo(email).Count.ShouldBe(2);
        withOld.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        withNew.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Resend_UnknownToken_ReturnsExpired()
    {
        // Act
        var response = await ResendAsync("nepostojeci-token");

        // Assert
        (await ErrorAsync(response)).GetProperty("code").GetString().ShouldBe(AccountService.RegistrationExpired);
    }

    [Fact]
    public async Task Register_MailServerDown_Returns503AndWorksOnceItIsBack()
    {
        // Arrange
        var email = UniqueEmail();
        factory.Emails.Fail = m => m.To == email ? new IOException("SMTP unreachable") : null;

        try
        {
            // Act
            var response = await factory.CreateClient().StartRegistrationAsync(email);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe(AccountService.EmailUnavailable);
            (await CountPendingAsync(email)).ShouldBe(0);
        }
        finally
        {
            factory.Emails.Fail = _ => null;
        }

        await factory.CreateClient().RegisterAsync(factory, email);
        (await LoginAsync(email)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Resend_MailServerDown_TheOldCodeKeepsWorking()
    {
        // Arrange
        var email = UniqueEmail();
        var token = await StartAsync(email);
        var code = factory.Emails.LatestCode(email);
        await AgeCodeAsync(email);
        factory.Emails.Fail = m => m.To == email ? new IOException("SMTP unreachable") : null;

        try
        {
            // Act
            var response = await ResendAsync(token);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await factory.CreateClient().ConfirmRegistrationAsync(token, code)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        finally
        {
            factory.Emails.Fail = _ => null;
        }
    }

    [Fact]
    public async Task Confirm_InvitedMeanwhile_ReturnsEmailTaken()
    {
        // Arrange: the code is still waiting when the same address gets into a company by invitation
        var email = UniqueEmail();
        var token = await StartAsync(email);
        var code = factory.Emails.LatestCode(email);
        var owner = await factory.CreateSignedInClientAsync();
        (await owner.PostAsJsonAsync("/api/account/invitations", new { email, role = "employee" })).EnsureSuccessStatusCode();
        var invitation = Regex.Match(factory.Emails.SentTo(email)[^1].Text, "/invite/([A-Za-z0-9_-]+)").Groups[1].Value;
        (await factory.CreateClient().PostAsJsonAsync($"/api/invitations/{invitation}/accept", new { name = "Ana", password = Password }))
            .EnsureSuccessStatusCode();

        // Act
        var response = await factory.CreateClient().ConfirmRegistrationAsync(token, code);

        // Assert
        (await ErrorAsync(response)).GetProperty("code").GetString().ShouldBe("auth.email_taken");
    }

    [Fact]
    public async Task Cleanup_AfterADay_DeletesOnlyAbandonedRegistrations()
    {
        // Arrange
        var abandoned = UniqueEmail();
        var current = UniqueEmail();
        await StartAsync(current);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PendingRegistrations.Add(new PendingRegistration(abandoned, "Firma", "Ana", Languages.Default, "hes", Invitation.NewToken(),
            PendingRegistration.NewCode(), DateTimeOffset.UtcNow - PendingRegistration.KeptFor));
        await db.SaveChangesAsync();

        // Act
        await scope.ServiceProvider.GetRequiredService<AccountService>().DeleteAbandonedRegistrationsAsync(CancellationToken.None);

        // Assert
        (await CountPendingAsync(abandoned)).ShouldBe(0);
        (await CountPendingAsync(current)).ShouldBe(1);
    }

    [Fact]
    public async Task Register_TooManyRequestsFromOneAddress_Returns429OnlyForThatAddress()
    {
        // Arrange: the test factory allows two requests an hour per address
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");
        var other = factory.CreateClient();
        other.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.8");

        // Act
        await client.StartRegistrationAsync(UniqueEmail());
        await client.StartRegistrationAsync(UniqueEmail());
        var overLimit = await client.StartRegistrationAsync(UniqueEmail());
        var fromOther = await other.StartRegistrationAsync(UniqueEmail());

        // Assert
        overLimit.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await overLimit.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.too_many_requests");
        fromOther.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
