using System.Globalization;
using System.Net;
using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Zalihe.Application.Channels;
using Zalihe.Application.Orders;
using Zalihe.Application.Webhooks;
using Zalihe.Domain.Channels;
using Zalihe.Infrastructure.Jobs;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Infrastructure.Tenancy;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Orders;

/// <summary>
/// The periodic sync that covers missed webhooks (SPEC 7.4), run the way its job runs it: in its own
/// scope with the company set, against the fake shop.
/// </summary>
[Collection(ApiCollection.Name)]
public class OrderSyncTests(ZaliheApiFactory factory)
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The shop answers the orders query with <paramref name="ordersJson"/> and records the request.</summary>
    private void ShopHasOrders(string ordersJson) =>
        factory.Shop.RespondWith(request => request.RequestUri!.AbsolutePath.EndsWith("/orders", StringComparison.Ordinal)
                                            && request.RequestUri.Query.Contains("&page=1", StringComparison.Ordinal)
            ? FakeShopHandler.Json(ordersJson)
            : FakeShopHandler.Json("[]"));

    private static string OrderJson(string status, DateTimeOffset modifiedAt, int productId, int quantity) =>
        JsonSerializer.Serialize(new
        {
            id = 1041,
            status,
            date_created_gmt = DateTimeOffset.UtcNow.AddMinutes(1).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            date_modified_gmt = modifiedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            line_items = new[] { new { id = 1, product_id = productId, variation_id = 0, quantity } },
        });

    private async Task<T> InScopeAsync<T>(Guid tenantId, Func<IServiceProvider, Task<T>> run)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await run(scope.ServiceProvider);
    }

    private async Task<SalesChannel> ChannelAsync(Guid channelId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SalesChannels
            .IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == channelId);
    }

    private async Task<OrderSyncResult> SyncAsync(Guid channelId, Guid? asTenant = null)
    {
        var tenantId = asTenant ?? (await ChannelAsync(channelId)).TenantId;
        return await InScopeAsync(tenantId, s => s.GetRequiredService<OrderSyncService>().SyncAsync(channelId, CancellationToken.None));
    }

    private static string? QueryValue(HttpRequestMessage request, string name) =>
        request.RequestUri!.Query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p[0] == name)
            .Select(p => Uri.UnescapeDataString(p[1]))
            .SingleOrDefault();

    [Fact]
    public async Task Sync_OrderMissedByWebhooks_LowersStockAndRemembersSync()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        var channel = await ChannelAsync(channelId);
        ShopHasOrders($"[{OrderJson("processing", T1, 10, 2)}]");
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = await SyncAsync(channelId);

        // Assert
        result.ShouldBe(new OrderSyncResult(OrderCount: 1, MovementCount: 1));
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(22);
        (await LinkedShop.OrderMovementsAsync(client, "KF-ETI-250")).ShouldHaveSingleItem()
            .GetProperty("externalRef").GetString().ShouldBe("1041");

        // The first sync starts at connecting; the customer's data isn't even requested.
        var request = factory.Shop.Requests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/wp-json/wc/v3/orders");
        QueryValue(request, "modified_after").ShouldBe(channel.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        QueryValue(request, "dates_are_gmt").ShouldBe("true");
        QueryValue(request, "status").ShouldBe("any");
        QueryValue(request, "_fields").ShouldBe("id,status,date_created_gmt,date_modified_gmt,line_items");

        (await ChannelAsync(channelId)).LastSyncedAt.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task Sync_OrderAlreadyAppliedByWebhook_DoesNotTakeStockTwice()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        var tenantId = (await ChannelAsync(channelId)).TenantId;
        await InScopeAsync(tenantId, s => s.GetRequiredService<OrderService>().ProcessAsync(
            channelId, new ShopOrder("1041", "processing", DateTimeOffset.UtcNow.AddMinutes(1), T1, [new ShopOrderLine("10", 2)]), CancellationToken.None));
        ShopHasOrders($"[{OrderJson("processing", T1, 10, 2)}]");

        // Act
        var result = await SyncAsync(channelId);

        // Assert
        result.ShouldBe(new OrderSyncResult(OrderCount: 1, MovementCount: 0));
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(22);
    }

    [Fact]
    public async Task Sync_CancellationMissedByWebhooks_ReturnsStock()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        ShopHasOrders($"[{OrderJson("processing", T1, 10, 2)}]");
        await SyncAsync(channelId);
        ShopHasOrders($"[{OrderJson("cancelled", T1.AddMinutes(20), 10, 2)}]");

        // Act
        var result = await SyncAsync(channelId);

        // Assert
        result.MovementCount.ShouldBe(1);
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(24);
    }

    [Fact]
    public async Task Sync_AfterEarlierSync_AsksFromItsStartMinusOverlap()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);
        ShopHasOrders("[]");
        await SyncAsync(channelId);
        var lastSync = (await ChannelAsync(channelId)).LastSyncedAt!.Value;
        ShopHasOrders("[]");

        // Act
        await SyncAsync(channelId);

        // Assert
        var expected = (lastSync - OrderSyncService.Overlap).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        QueryValue(factory.Shop.Requests.ShouldHaveSingleItem(), "modified_after").ShouldBe(expected);
    }

    [Fact]
    public async Task Sync_ShopRejectsKeys_ThrowsAndKeepsLastSync()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);
        factory.Shop.RespondWith(_ => FakeShopHandler.Json("""{"code":"woocommerce_rest_cannot_view"}""", HttpStatusCode.Unauthorized));

        // Act
        var error = await Should.ThrowAsync<SalesChannelException>(() => SyncAsync(channelId));

        // Assert
        error.Code.ShouldBe(SalesChannelException.Unauthorized);
        (await ChannelAsync(channelId)).LastSyncedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Sync_OtherCompanysShop_DoesNothing()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);
        var (_, otherChannelId) = await LinkedShop.CreateAsync(factory);
        var otherTenantId = (await ChannelAsync(otherChannelId)).TenantId;
        ShopHasOrders($"[{OrderJson("processing", T1, 10, 2)}]");

        // Act
        var result = await SyncAsync(channelId, asTenant: otherTenantId);

        // Assert
        result.ShouldBe(new OrderSyncResult(0, 0));
        factory.Shop.Requests.ShouldBeEmpty();
        (await ChannelAsync(channelId)).LastSyncedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ScheduleAll_ShopsOfSeveralCompanies_SchedulesASyncForEachWithItsCompany()
    {
        // Arrange
        var (_, first) = await LinkedShop.CreateAsync(factory);
        var (_, second) = await LinkedShop.CreateAsync(factory);
        var jobs = Substitute.For<IBackgroundJobClient>();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var job = ActivatorUtilities.CreateInstance<OrderSyncJob>(services, jobs);

        // Act
        await job.ScheduleAllAsync(CancellationToken.None);

        // Assert
        var scheduled = jobs.ReceivedCalls()
            .Select(c => (Job)c.GetArguments()[0]!)
            .Select(j => (TenantId: (Guid)j.Args[0], ChannelId: (Guid)j.Args[1]))
            .ToList();
        scheduled.ShouldContain(((await ChannelAsync(first)).TenantId, first));
        scheduled.ShouldContain(((await ChannelAsync(second)).TenantId, second));
        jobs.ReceivedCalls().ShouldAllBe(c => c.GetArguments()[1] is EnqueuedState);
    }

    [Fact]
    public async Task DeleteExpired_EventsOfSeveralCompanies_DeletesOnlyThoseOlderThan30Days()
    {
        // Arrange
        var (_, first) = await LinkedShop.CreateAsync(factory);
        var (_, second) = await LinkedShop.CreateAsync(factory);
        var now = DateTimeOffset.UtcNow;
        var ids = new List<(Guid Id, bool Expired)>();
        foreach (var channelId in new[] { first, second })
        {
            var channel = await ChannelAsync(channelId);
            await InScopeAsync(channel.TenantId, async s =>
            {
                var db = s.GetRequiredService<AppDbContext>();
                var old = new WebhookEvent(channel, "order.updated", "{}", now - WebhookService.Retention - TimeSpan.FromHours(1));
                var recent = new WebhookEvent(channel, "order.updated", "{}", now - WebhookService.Retention + TimeSpan.FromHours(1));
                db.WebhookEvents.AddRange(old, recent);
                ids.Add((old.Id, true));
                ids.Add((recent.Id, false));
                return await db.SaveChangesAsync();
            });
        }

        // Act: as the job runs it, without a company.
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WebhookService>().DeleteExpiredAsync(CancellationToken.None);
        }

        // Assert
        using var check = factory.Services.CreateScope();
        var allIds = ids.Select(e => e.Id).ToList();
        var left = await check.ServiceProvider.GetRequiredService<AppDbContext>().WebhookEvents
            .IgnoreQueryFilters().Where(e => allIds.Contains(e.Id)).Select(e => e.Id).ToListAsync();
        left.ShouldBe(ids.Where(e => !e.Expired).Select(e => e.Id), ignoreOrder: true);
    }
}
