using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.Application.Feedback;
using Zalihe.Domain.Feedback;
using Zalihe.Infrastructure.Email;
using Zalihe.IntegrationTests.Infrastructure;
using static Zalihe.IntegrationTests.Infrastructure.HttpClientExtensions;

namespace Zalihe.IntegrationTests.Feedback;

/// <summary>Messages from users to the platform's owner (SPEC 11.10): /api/feedback and /api/platform/feedback.</summary>
[Collection(ApiCollection.Name)]
public class FeedbackTests(ZaliheApiFactory factory)
{
    private const string Password = "sigurna-lozinka";
    private const string Owner = ZaliheApiFactory.PlatformOwnerEmail;

    /// <summary>Signs in as the platform's owner, registering the owner's company if no test has yet.</summary>
    private async Task<HttpClient> OwnerAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = Owner, password = Password });
        if (login.StatusCode != HttpStatusCode.NoContent)
        {
            await client.RegisterAsync(factory, Owner, Password, "Firma vlasnika platforme", "Vlasnik");
        }

        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", await client.GetAntiforgeryTokenAsync());
        return client;
    }

    /// <summary>A signed-in admin of a new company with a name no other test uses, and the admin's email.</summary>
    private async Task<(HttpClient Client, string Company, string Email)> UserAsync()
    {
        var company = $"Firma {Guid.NewGuid():N}";
        var email = UniqueEmail();
        var client = factory.CreateClient();
        await client.RegisterAsync(factory, email, Password, company, "Ana");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", await client.GetAntiforgeryTokenAsync());
        return (client, company, email);
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    /// <summary>Sends the form the web app sends; <paramref name="screenshot"/> goes as the file "screenshot".</summary>
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string message, string kind = "suggestion",
        bool mayContact = true, byte[]? screenshot = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(kind), "kind" },
            { new StringContent(message), "message" },
            { new StringContent("/items"), "page" },
            { new StringContent(mayContact ? "true" : "false"), "mayContact" },
        };
        if (screenshot is not null) form.Add(new ByteArrayContent(screenshot), "screenshot", "screen.png");
        return client.PostAsync("/api/feedback", form);
    }

    private static async Task<JsonElement?> FindAsync(HttpClient owner, string message, string? status = null)
    {
        var page = await owner.GetFromJsonAsync<JsonElement>($"/api/platform/feedback?pageSize=100{(status is null ? "" : $"&status={status}")}");
        return page.GetProperty("items").EnumerateArray().Cast<JsonElement?>()
            .FirstOrDefault(f => f!.Value.GetProperty("message").GetString() == message);
    }

    /// <summary>The owner's email with <paramref name="text"/>, which a background job sends a moment after the message.</summary>
    private async Task<EmailMessage?> OwnerEmailWithAsync(string text)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (factory.Emails.SentTo(Owner).FirstOrDefault(m => m.Text.Contains(text)) is { } sent) return sent;
            await Task.Delay(100);
        }

        return null;
    }

    [Fact]
    public async Task Send_Suggestion_OwnerSeesItWithCompanyPageAndContact()
    {
        // Arrange
        var (client, company, email) = await UserAsync();
        var message = $"Show the purchase price {Guid.NewGuid():N}";
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh) Chrome/131.0");

        // Act
        var response = await SendAsync(client, message);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var found = (await FindAsync(await OwnerAsync(), message, "new")).ShouldNotBeNull();
        found.GetProperty("kind").GetString().ShouldBe("suggestion");
        found.GetProperty("status").GetString().ShouldBe("new");
        found.GetProperty("company").GetString().ShouldBe(company);
        found.GetProperty("userName").GetString().ShouldBe("Ana");
        found.GetProperty("contactEmail").GetString().ShouldBe(email);
        found.GetProperty("page").GetString().ShouldBe("/items");
        found.GetProperty("client").GetString()!.ShouldContain("Chrome/131.0");
    }

    [Fact]
    public async Task Send_NoContactAllowed_HidesTheEmail()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        var message = $"Hide my email {Guid.NewGuid():N}";

        // Act
        await SendAsync(client, message, "problem", mayContact: false);

        // Assert
        var found = (await FindAsync(await OwnerAsync(), message)).ShouldNotBeNull();
        found.GetProperty("contactEmail").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Send_Suggestion_EmailsThePlatformOwner()
    {
        // Arrange
        var (client, company, email) = await UserAsync();
        var message = $"Daily sales on the home page {Guid.NewGuid():N}";

        // Act
        await SendAsync(client, message, "confusing");

        // Assert
        var sent = (await OwnerEmailWithAsync(message)).ShouldNotBeNull();
        sent.Subject.ShouldBe($"Suvišno ili zbunjuje · {company}");
        sent.Text.ShouldContain($"Od: Ana, {company} · može kontakt: {email}");
        sent.Text.ShouldContain("Stranica: /items");
    }

    [Theory]
    [InlineData("", "suggestion", "message", "validation.required")]
    [InlineData("   ", "suggestion", "message", "validation.required")]
    [InlineData("Text", "prompt", "kind", "validation.invalid")]
    public async Task Send_InvalidRequest_ReturnsFieldError(string message, string kind, string field, string code)
    {
        // Arrange
        var (client, _, _) = await UserAsync();

        // Act
        var response = await SendAsync(client, message, kind);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ToList();
        errors.ShouldContain(e => e.GetProperty("field").GetString() == field && e.GetProperty("code").GetString() == code);
    }

    [Fact]
    public async Task Send_OverHourlyLimit_IsTooManyRequests()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        for (var i = 0; i < FeedbackService.PerHourLimit; i++)
        {
            (await SendAsync(client, $"Message {i}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Act
        var response = await SendAsync(client, "One too many");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("feedback.too_many");
    }

    [Fact]
    public async Task Send_NotSignedIn_IsUnauthorized()
    {
        // Act
        var response = await SendAsync(factory.CreateClient(), "Hello");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnswerPrompt_Once_IsListedAsAnsweredAndNotAskedAgain()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        (await client.GetFromJsonAsync<string[]>("/api/feedback/prompts")).ShouldBeEmpty();

        // Act
        var first = await client.PostAsJsonAsync("/api/feedback/prompts/csvImport", new { positive = true, page = "/items/import" });
        var second = await client.PostAsJsonAsync("/api/feedback/prompts/csvImport", new { positive = false, message = "Changed my mind" });

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<string[]>("/api/feedback/prompts")).ShouldBe(["csvImport"]);
        (await FindAsync(await OwnerAsync(), "Changed my mind")).ShouldBeNull();
    }

    [Fact]
    public async Task AnswerPrompt_PlainYes_IsCountedButNotListed()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        var owner = await OwnerAsync();
        var before = await owner.GetFromJsonAsync<JsonElement>("/api/platform/feedback/summary");

        // Act
        await client.PostAsJsonAsync("/api/feedback/prompts/csvImport", new { positive = true });

        // Assert
        var after = await owner.GetFromJsonAsync<JsonElement>("/api/platform/feedback/summary");
        after.GetProperty("csvImportAnswers").GetInt32().ShouldBe(before.GetProperty("csvImportAnswers").GetInt32() + 1);
        after.GetProperty("csvImportClear").GetInt32().ShouldBe(before.GetProperty("csvImportClear").GetInt32() + 1);
        after.GetProperty("new").GetInt32().ShouldBe(before.GetProperty("new").GetInt32());
    }

    [Fact]
    public async Task AnswerPrompt_NotReallyWithText_IsListedAsNew()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        var message = $"Which column is the minimum {Guid.NewGuid():N}";

        // Act
        await client.PostAsJsonAsync("/api/feedback/prompts/csvImport", new { positive = false, message });

        // Assert
        var found = (await FindAsync(await OwnerAsync(), message, "new")).ShouldNotBeNull();
        found.GetProperty("kind").GetString().ShouldBe("prompt");
        found.GetProperty("prompt").GetString().ShouldBe("csvImport");
        found.GetProperty("promptAnswer").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task SetStatus_Done_MovesTheMessageOutOfNew()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        var message = $"Mark me done {Guid.NewGuid():N}";
        await SendAsync(client, message);
        var owner = await OwnerAsync();
        var id = (await FindAsync(owner, message)).ShouldNotBeNull().GetProperty("id").GetString();

        // Act
        var response = await owner.PutAsJsonAsync($"/api/platform/feedback/{id}/status", new { status = "done" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindAsync(owner, message, "new")).ShouldBeNull();
        (await FindAsync(owner, message, "done")).ShouldNotBeNull();
    }

    [Fact]
    public async Task SetStatus_UnknownMessage_IsNotFound()
    {
        // Act
        var response = await (await OwnerAsync()).PutAsJsonAsync($"/api/platform/feedback/{Guid.NewGuid()}/status", new { status = "read" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("GET", "/api/platform/feedback")]
    [InlineData("GET", "/api/platform/feedback/summary")]
    [InlineData("PUT", "/api/platform/feedback/00000000-0000-0000-0000-000000000001/status")]
    public async Task Platform_CompanyAdmin_IsForbidden(string method, string path)
    {
        // Arrange
        var (client, _, _) = await UserAsync();

        // Act
        var response = method == "GET" ? await client.GetAsync(path) : await client.PutAsJsonAsync(path, new { status = "read" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Send_WithScreenshot_OwnerSeesItUntilDone()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        var message = $"With a picture {Guid.NewGuid():N}";

        // Act
        var response = await SendAsync(client, message, "problem", screenshot: Png);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var owner = await OwnerAsync();
        var found = (await FindAsync(owner, message)).ShouldNotBeNull();
        found.GetProperty("hasScreenshot").GetBoolean().ShouldBeTrue();
        var id = found.GetProperty("id").GetString();
        var picture = await owner.GetAsync($"/api/platform/feedback/{id}/screenshot");
        picture.StatusCode.ShouldBe(HttpStatusCode.OK);
        picture.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await picture.Content.ReadAsByteArrayAsync()).ShouldBe(Png);
        (await OwnerEmailWithAsync(message)).ShouldNotBeNull().Text.ShouldContain("uz poruku je slika ekrana");

        // Done deletes the picture.
        await owner.PutAsJsonAsync($"/api/platform/feedback/{id}/status", new { status = "done" });
        (await owner.GetAsync($"/api/platform/feedback/{id}/screenshot")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await FindAsync(owner, message)).ShouldNotBeNull().GetProperty("hasScreenshot").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Send_ScreenshotNotAPicture_IsRefusedAndNothingSaved()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        var message = $"Bad picture {Guid.NewGuid():N}";

        // Act
        var response = await SendAsync(client, message, screenshot: "<svg onload=alert(1)>"u8.ToArray());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("feedback.screenshot_unsupported");
        error.GetProperty("field").GetString().ShouldBe("screenshot");
        (await FindAsync(await OwnerAsync(), message)).ShouldBeNull();
    }

    [Fact]
    public async Task Send_ScreenshotTooLarge_IsRefused()
    {
        // Arrange
        var (client, _, _) = await UserAsync();
        var large = new byte[FeedbackScreenshot.MaxBytes + 1];
        Png.CopyTo(large, 0);

        // Act
        var response = await SendAsync(client, "Huge", screenshot: large);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem()
            .GetProperty("code").GetString().ShouldBe("feedback.screenshot_too_large");
    }

    [Fact]
    public async Task Screenshot_CompanyAdmin_IsForbidden()
    {
        // Arrange
        var (client, _, _) = await UserAsync();

        // Act
        var response = await client.GetAsync($"/api/platform/feedback/{Guid.NewGuid()}/screenshot");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnsweredPrompts_OtherCompany_DoesNotSeeTheirAnswers()
    {
        // Arrange
        var (a, _, _) = await UserAsync();
        var (b, _, _) = await UserAsync();
        await a.PostAsJsonAsync("/api/feedback/prompts/csvImport", new { positive = true });

        // Act
        var answeredByB = await b.GetFromJsonAsync<string[]>("/api/feedback/prompts");

        // Assert
        answeredByB.ShouldBeEmpty();
    }
}
