using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Application.Alerts;
using Zalihe.Application.Channels;
using Zalihe.Application.Orders;
using Zalihe.Domain.Channels;
using Zalihe.Infrastructure.Email;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Infrastructure.Tenancy;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Alerts;

[Collection(ApiCollection.Name)]
public class ChannelFailureAlertTests(ZaliheApiFactory factory)
{
    private sealed record Shop(HttpClient Client, Guid ChannelId, Guid TenantId, string Email);

    private async Task<Shop> ShopAsync(string? language = null)
    {
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        if (language is not null)
        {
            (await client.PutAsJsonAsync("/api/account/profile", new { name = "Miljan", language })).EnsureSuccessStatusCode();
        }

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        return new Shop(client, channelId, me.GetProperty("tenantId").GetGuid(), me.GetProperty("email").GetString()!);
    }

    private async Task<T> InScopeAsync<T>(Shop shop, Func<IServiceProvider, Task<T>> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(shop.TenantId);
        return await run(scope.ServiceProvider);
    }

    private Task SyncAsync(Shop shop) =>
        InScopeAsync(shop, s => s.GetRequiredService<OrderSyncService>().SyncAsync(shop.ChannelId, CancellationToken.None));

    private Task<bool> SendAsync(Shop shop) =>
        InScopeAsync(shop, s => s.GetRequiredService<ChannelFailureAlertService>().SendAsync(shop.ChannelId, CancellationToken.None));

    /// <summary>The shop has been failing for longer than <see cref="SalesChannel.ErrorAfter"/>; the next failure shows the error.</summary>
    private async Task FailingForAWhileAsync(Shop shop, HttpStatusCode status = HttpStatusCode.Unauthorized)
    {
        factory.Shop.RespondWith(_ => FakeShopHandler.Json("""{"code":"woocommerce_rest_cannot_view"}""", status));
        await Should.ThrowAsync<SalesChannelException>(() => SyncAsync(shop));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""UPDATE "SalesChannels" SET "FailingSince" = now() - interval '31 minutes' WHERE "Id" = {shop.ChannelId}""");
    }

    private async Task<SalesChannel> ChannelAsync(Shop shop)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SalesChannels.IgnoreQueryFilters()
            .AsNoTracking().SingleAsync(c => c.Id == shop.ChannelId);
    }

    private async Task<IReadOnlyList<EmailMessage>> WaitForEmailsAsync(string to, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (factory.Emails.SentTo(to).Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        return factory.Emails.SentTo(to);
    }

    [Fact]
    public async Task Sync_ShopFailingForHalfAnHour_EmailsTheOwnerOnceWithTheReason()
    {
        // Arrange
        var shop = await ShopAsync();
        await FailingForAWhileAsync(shop);

        // Act: this failure shows the error, and a background job sends the email.
        await Should.ThrowAsync<SalesChannelException>(() => SyncAsync(shop));

        // Assert
        var email = (await WaitForEmailsAsync(shop.Email, 1)).ShouldHaveSingleItem();
        email.Subject.ShouldBe("Test Firma · Prodavnica ne radi: zrno.rs");
        email.Text.ShouldContain("Prodavnica je odbila ključeve.");
        email.Html.ShouldContain("https://app.zalihe.test/channels");
        var channel = await ChannelAsync(shop);
        channel.Status.ShouldBe(SalesChannelStatus.Error);
        channel.ErrorEmailedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Send_Again_DoesNotSendASecondEmail()
    {
        // Arrange
        var shop = await ShopAsync();
        await FailingForAWhileAsync(shop);
        await Should.ThrowAsync<SalesChannelException>(() => SyncAsync(shop));
        await WaitForEmailsAsync(shop.Email, 1);

        // Act: a retried job, or more failed syncs.
        var again = await SendAsync(shop);
        await Should.ThrowAsync<SalesChannelException>(() => SyncAsync(shop));

        // Assert
        again.ShouldBeFalse();
        await Task.Delay(500);
        factory.Emails.SentTo(shop.Email).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sync_ShopWorksThenFailsAgain_SendsANewEmail()
    {
        // Arrange
        var shop = await ShopAsync();
        await FailingForAWhileAsync(shop);
        await Should.ThrowAsync<SalesChannelException>(() => SyncAsync(shop));
        await WaitForEmailsAsync(shop.Email, 1);
        factory.Shop.RespondWith(_ => FakeShopHandler.Json("[]"));
        await SyncAsync(shop);

        // Act
        await FailingForAWhileAsync(shop, HttpStatusCode.ServiceUnavailable);
        await Should.ThrowAsync<SalesChannelException>(() => SyncAsync(shop));

        // Assert
        var emails = await WaitForEmailsAsync(shop.Email, 2);
        emails.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Send_ShopWorking_SendsNothing()
    {
        var shop = await ShopAsync();

        (await SendAsync(shop)).ShouldBeFalse();

        factory.Emails.SentTo(shop.Email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Send_UserInEnglish_GetsEnglishTexts()
    {
        // Arrange
        var shop = await ShopAsync(language: "en");
        await FailingForAWhileAsync(shop);
        await Should.ThrowAsync<SalesChannelException>(() => SyncAsync(shop));

        // Act
        var email = (await WaitForEmailsAsync(shop.Email, 1)).ShouldHaveSingleItem();

        // Assert
        email.Subject.ShouldBe("Test Firma · Shop not working: zrno.rs");
        email.Text.ShouldContain("The shop rejected the keys.");
    }
}
