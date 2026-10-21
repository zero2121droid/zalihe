using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Application.Alerts;
using Zalihe.Application.Common;
using Zalihe.Infrastructure.Tenancy;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Alerts;

[Collection(ApiCollection.Name)]
public class LowStockAlertTests(ZaliheApiFactory factory)
{
    /// <summary>9:00 in Belgrade on a given October day: inside the hours emails go out.</summary>
    private static DateTimeOffset Morning(int day) => new(2026, 10, day, 9, 0, 0, TimeSpan.FromHours(2));

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private sealed record Company(HttpClient Client, string Email, Guid TenantId);

    private async Task<Company> CompanyAsync(string? language = null)
    {
        var client = await factory.CreateSignedInClientAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        if (language is not null)
        {
            (await client.PutAsJsonAsync("/api/account/profile", new { name = "Miljan", language })).EnsureSuccessStatusCode();
        }

        return new Company(client, me.GetProperty("email").GetString()!, me.GetProperty("tenantId").GetGuid());
    }

    private static async Task<string> ItemAsync(Company company, string name, string sku, decimal minStock, decimal stock)
    {
        var response = await company.Client.PostAsJsonAsync("/api/items", new { name, sku, unit = "kom", minStock });
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        if (stock > 0)
        {
            (await company.Client.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "receipt", quantity = stock })).EnsureSuccessStatusCode();
        }

        return id;
    }

    /// <summary>Runs the alert for the company at <paramref name="now"/>, as the hourly job would.</summary>
    private async Task<LowStockAlertOutcome> RunAsync(Company company, DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tenant = services.GetRequiredService<TenantContext>();
        tenant.Set(company.TenantId);
        var service = new LowStockAlertService(
            services.GetRequiredService<IAppDbContext>(), tenant, services.GetRequiredService<IAlertEmails>(), new FixedTime(now));
        return await service.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Run_ItemsLowAndOut_EmailsTheListMostUrgentFirst()
    {
        // Arrange
        var company = await CompanyAsync();
        await ItemAsync(company, "Filter papir V60", "FP-V60", minStock: 15, stock: 6);
        await ItemAsync(company, "Džezva bakarna", "DZ-BAK", minStock: 3, stock: 0);
        await ItemAsync(company, "Kafa Etiopija", "KF-ETI", minStock: 10, stock: 40);
        var inactive = await ItemAsync(company, "Stari mlin", "ML-OLD", minStock: 5, stock: 0);
        await company.Client.PostAsync($"/api/items/{inactive}/deactivate", null);

        // Act
        var outcome = await RunAsync(company, Morning(19));

        // Assert
        outcome.ShouldBe(LowStockAlertOutcome.Sent);
        var email = factory.Emails.SentTo(company.Email).ShouldHaveSingleItem();
        email.Subject.ShouldBe("Test Firma · Treba naručiti: 2");
        email.Text.IndexOf("Džezva bakarna", StringComparison.Ordinal).ShouldBeLessThan(email.Text.IndexOf("Filter papir V60", StringComparison.Ordinal));
        email.Text.ShouldContain("Filter papir V60 (FP-V60): 6 kom, Min. 15 kom [novo]");
        email.Text.ShouldContain("Džezva bakarna (DZ-BAK): 0 kom, Min. 3 kom [novo, nema na stanju]");
        email.Text.ShouldNotContain("Kafa Etiopija");
        email.Text.ShouldNotContain("Stari mlin");
        email.Html.ShouldContain("https://app.zalihe.test/items?status=low");
        email.Html.ShouldContain("<img src=\"https://app.zalihe.test/email-logo.png\"");
    }

    [Fact]
    public async Task Run_AgainTheSameDayAndNextDayWithNothingNew_SendsNoMoreEmails()
    {
        // Arrange
        var company = await CompanyAsync();
        await ItemAsync(company, "Filter papir V60", "FP-V60", minStock: 15, stock: 6);
        await RunAsync(company, Morning(19));

        // Act
        var later = await RunAsync(company, Morning(19).AddHours(5));
        var nextDay = await RunAsync(company, Morning(20));

        // Assert
        later.ShouldBe(LowStockAlertOutcome.NotDue);
        nextDay.ShouldBe(LowStockAlertOutcome.NotDue);
        factory.Emails.SentTo(company.Email).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Run_AnotherItemFallsLaterThatDay_IsInTomorrowsEmailMarkedNew()
    {
        // Arrange
        var company = await CompanyAsync();
        await ItemAsync(company, "Filter papir V60", "FP-V60", minStock: 15, stock: 6);
        await RunAsync(company, Morning(19));
        var coffee = await ItemAsync(company, "Kafa Etiopija", "KF-ETI", minStock: 10, stock: 12);
        (await company.Client.PostAsJsonAsync($"/api/items/{coffee}/movements", new { kind = "sale", quantity = 5 })).EnsureSuccessStatusCode();

        // Act
        var sameDay = await RunAsync(company, Morning(19).AddHours(3));
        var nextDay = await RunAsync(company, Morning(20));

        // Assert
        sameDay.ShouldBe(LowStockAlertOutcome.NotDue);
        nextDay.ShouldBe(LowStockAlertOutcome.Sent);
        var email = factory.Emails.SentTo(company.Email)[^1];
        email.Text.ShouldContain("Kafa Etiopija (KF-ETI): 7 kom, Min. 10 kom [novo]");
        email.Text.ShouldContain("Filter papir V60 (FP-V60): 6 kom, Min. 15 kom" + Environment.NewLine);
    }

    [Fact]
    public async Task Run_UserInEnglish_GetsEnglishTextsAndNumbers()
    {
        // Arrange
        var company = await CompanyAsync(language: "en");
        await ItemAsync(company, "Coffee beans", "KF-1", minStock: 1500, stock: 1234.5m);

        // Act
        await RunAsync(company, Morning(19));

        // Assert
        var email = factory.Emails.SentTo(company.Email).ShouldHaveSingleItem();
        email.Subject.ShouldBe("Test Firma · To reorder: 1");
        email.Text.ShouldContain("Coffee beans (KF-1): 1,234.5 pcs, Min. 1,500 pcs [new]");
    }

    [Fact]
    public async Task Run_AtNight_WaitsForTheMorning()
    {
        // Arrange
        var company = await CompanyAsync();
        await ItemAsync(company, "Filter papir V60", "FP-V60", minStock: 15, stock: 6);

        // Act
        var night = await RunAsync(company, Morning(19).AddHours(14)); // 23:00
        var morning = await RunAsync(company, Morning(20).AddHours(-2)); // 7:00

        // Assert
        night.ShouldBe(LowStockAlertOutcome.NotDue);
        morning.ShouldBe(LowStockAlertOutcome.Sent);
    }

    [Fact]
    public async Task Run_MailServerDown_TriesAgainNextRun()
    {
        // Arrange
        var company = await CompanyAsync();
        await ItemAsync(company, "Filter papir V60", "FP-V60", minStock: 15, stock: 6);
        factory.Emails.Fail = m => m.To == company.Email ? new IOException("SMTP unreachable") : null;

        // Act
        try
        {
            await Should.ThrowAsync<IOException>(() => RunAsync(company, Morning(19)));
        }
        finally
        {
            factory.Emails.Fail = _ => null;
        }

        var retry = await RunAsync(company, Morning(19).AddHours(1));

        // Assert
        retry.ShouldBe(LowStockAlertOutcome.Sent);
        factory.Emails.SentTo(company.Email).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Run_TwoCompanies_EachGetsOnlyItsOwnItems()
    {
        // Arrange
        var first = await CompanyAsync();
        var second = await CompanyAsync();
        await ItemAsync(first, "Filter papir V60", "FP-V60", minStock: 15, stock: 6);
        await ItemAsync(second, "Tuđi artikal", "TUDJE", minStock: 5, stock: 1);

        // Act
        await RunAsync(first, Morning(19));

        // Assert
        factory.Emails.SentTo(first.Email).ShouldHaveSingleItem().Text.ShouldNotContain("Tuđi artikal");
        factory.Emails.SentTo(second.Email).ShouldBeEmpty();
    }
}
