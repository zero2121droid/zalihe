using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Application.Channels;
using Zalihe.Application.Orders;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Infrastructure.Tenancy;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Orders;

/// <summary>The list of shop orders (GET /api/orders). Orders are applied the way their jobs apply them.</summary>
[Collection(ApiCollection.Name)]
public class OrdersTests(ZaliheApiFactory factory)
{
    private async Task<T> AsShopsCompanyAsync<T>(Guid channelId, Func<IServiceProvider, Task<T>> run)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var tenantId = await services.GetRequiredService<AppDbContext>().FindChannelTenantAsync(channelId, CancellationToken.None);
        services.GetRequiredService<TenantContext>().Set(tenantId!.Value);
        return await run(services);
    }

    private Task<ProcessOrderResult> ProcessAsync(Guid channelId, string number, string status, DateTimeOffset placedAt, params ShopOrderLine[] lines) =>
        AsShopsCompanyAsync(channelId, s => s.GetRequiredService<OrderService>()
            .ProcessAsync(channelId, new ShopOrder(number, status, placedAt, DateTimeOffset.UtcNow, lines), CancellationToken.None));

    /// <summary>Products are linked when a test starts, so orders placed from now on take stock.</summary>
    private static DateTimeOffset Placed(int minutesFromNow) => DateTimeOffset.UtcNow.AddMinutes(minutesFromNow);

    private static async Task<JsonElement> ListAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/orders{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<string?> Numbers(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("number").GetString()).ToList();

    [Fact]
    public async Task List_ProcessedOrders_NewestFirstWithProductsAndStockTaken()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await ProcessAsync(channelId, "1041", "processing", Placed(1), new ShopOrderLine("10", 2, "Kafa Etiopija"), new ShopOrderLine("99", 4, "Čaj od nane"));
        await ProcessAsync(channelId, "1042", "cancelled", Placed(2), new ShopOrderLine("51", 1, "Majica basic - M"));

        // Act
        var page = await ListAsync(client);

        // Assert
        page.GetProperty("totalCount").GetInt32().ShouldBe(2);
        Numbers(page).ShouldBe(["1042", "1041"]);

        var cancelled = page.GetProperty("items")[0];
        cancelled.GetProperty("status").GetString().ShouldBe("cancelled");
        cancelled.GetProperty("takesStock").GetBoolean().ShouldBeFalse();
        cancelled.GetProperty("lines")[0].GetProperty("appliedQuantity").GetDecimal().ShouldBe(0);

        var order = page.GetProperty("items")[1];
        order.GetProperty("channelId").GetGuid().ShouldBe(channelId);
        order.GetProperty("shopUrl").GetString().ShouldBe("https://zrno.rs");
        order.GetProperty("takesStock").GetBoolean().ShouldBeTrue();
        order.GetProperty("unlinkedLineCount").GetInt32().ShouldBe(1);

        var lines = order.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("externalProductId").GetString()!);
        var coffee = lines["10"];
        coffee.GetProperty("state").GetString().ShouldBe("linked");
        coffee.GetProperty("name").GetString().ShouldBe("Kafa Etiopija");
        coffee.GetProperty("item").GetProperty("sku").GetString().ShouldBe("KF-ETI-250");
        coffee.GetProperty("item").GetProperty("unit").GetString().ShouldBe("kom");
        coffee.GetProperty("orderedQuantity").GetDecimal().ShouldBe(2);
        coffee.GetProperty("appliedQuantity").GetDecimal().ShouldBe(2);

        var tea = lines["99"];
        tea.GetProperty("state").GetString().ShouldBe("unlinked");
        tea.GetProperty("name").GetString().ShouldBe("Čaj od nane");
        tea.GetProperty("item").ValueKind.ShouldBe(JsonValueKind.Null);
        tea.GetProperty("appliedQuantity").GetDecimal().ShouldBe(0);

        // Nothing about the customer is part of an order.
        order.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["id", "number", "channelId", "shopUrl", "status", "takesStock", "placedAt", "unlinkedLineCount", "lines"]);
    }

    [Fact]
    public async Task List_ProductRemovedFromOrder_IsLeftOut()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        var placed = Placed(1);
        await ProcessAsync(channelId, "1041", "processing", placed, new ShopOrderLine("10", 2), new ShopOrderLine("51", 1));
        await ProcessAsync(channelId, "1041", "processing", placed, new ShopOrderLine("10", 2));

        // Act
        var page = await ListAsync(client);

        // Assert
        page.GetProperty("items")[0].GetProperty("lines").EnumerateArray()
            .Select(l => l.GetProperty("externalProductId").GetString()).ShouldBe(["10"]);
    }

    [Fact]
    public async Task List_OrderPlacedBeforeLinking_ShowsItemWithoutCountingAsUnlinked()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await ProcessAsync(channelId, "1041", "completed", DateTimeOffset.UtcNow.AddDays(-2), new ShopOrderLine("10", 2));

        // Act
        var all = await ListAsync(client);
        var unlinked = await ListAsync(client, "?unlinkedOnly=true");

        // Assert
        var order = all.GetProperty("items")[0];
        order.GetProperty("unlinkedLineCount").GetInt32().ShouldBe(0);
        var line = order.GetProperty("lines")[0];
        line.GetProperty("state").GetString().ShouldBe("linkedAfterOrder");
        line.GetProperty("item").GetProperty("sku").GetString().ShouldBe("KF-ETI-250");
        line.GetProperty("appliedQuantity").GetDecimal().ShouldBe(0);
        unlinked.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task List_UnlinkedOnly_KeepsOrdersWithProductsToLink()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await ProcessAsync(channelId, "1041", "processing", Placed(1), new ShopOrderLine("10", 1));
        await ProcessAsync(channelId, "1042", "processing", Placed(2), new ShopOrderLine("10", 1), new ShopOrderLine("99", 1));

        // Act
        var page = await ListAsync(client, "?unlinkedOnly=true");

        // Assert
        Numbers(page).ShouldBe(["1042"]);
    }

    [Theory]
    [InlineData("?search=1042", "1042")]
    [InlineData("?search=%231042", "1042")]
    [InlineData("?search=%C4%8DAJ", "1041")] // "čAJ": product name, any case
    [InlineData("?status=cancelled", "1042")]
    public async Task List_SearchOrStatus_KeepsMatchingOrders(string query, string expected)
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await ProcessAsync(channelId, "1041", "processing", Placed(1), new ShopOrderLine("10", 1), new ShopOrderLine("99", 1, "Čaj od nane"));
        await ProcessAsync(channelId, "1042", "cancelled", Placed(2), new ShopOrderLine("10", 1, "Kafa Etiopija"));

        // Act
        var page = await ListAsync(client, query);

        // Assert
        Numbers(page).ShouldBe([expected]);
    }

    [Fact]
    public async Task List_ManyOrders_IsPaged()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        for (var i = 1; i <= 3; i++)
        {
            await ProcessAsync(channelId, $"10{i}", "processing", Placed(i), new ShopOrderLine("10", 1));
        }

        // Act
        var page = await ListAsync(client, "?page=2&pageSize=2");

        // Assert
        page.GetProperty("totalCount").GetInt32().ShouldBe(3);
        Numbers(page).ShouldBe(["101"]);
    }

    [Fact]
    public async Task List_PageSizeTooLarge_ReturnsValidationProblem()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.GetAsync("/api/orders?pageSize=101");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_OrdersOfAnotherCompany_AreNotVisible()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);
        await ProcessAsync(channelId, "1041", "processing", Placed(1), new ShopOrderLine("10", 1));
        var (otherClient, _) = await LinkedShop.CreateAsync(factory);

        // Act
        var page = await ListAsync(otherClient);

        // Assert
        page.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task List_SignedOut_ReturnsUnauthorized()
    {
        // Act
        var response = await factory.CreateClient().GetAsync("/api/orders");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sync_OrderFromShop_KeepsWhenPlacedAndProductNames()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        var placed = new DateTimeOffset(DateTime.UtcNow.AddMinutes(1).Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        string Gmt(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var order = JsonSerializer.Serialize(new
        {
            id = 1041,
            status = "processing",
            date_created_gmt = Gmt(placed),
            date_modified_gmt = Gmt(placed),
            line_items = new[] { new { id = 1, name = "Majica basic - M, Siva", product_id = 50, variation_id = 51, quantity = 1 } },
        });
        factory.Shop.RespondWith(r => FakeShopHandler.Json(r.RequestUri!.Query.Contains("&page=1", StringComparison.Ordinal) ? $"[{order}]" : "[]"));

        // Act
        await AsShopsCompanyAsync(channelId, s => s.GetRequiredService<OrderSyncService>().SyncAsync(channelId, CancellationToken.None));

        // Assert
        var listed = (await ListAsync(client)).GetProperty("items")[0];
        listed.GetProperty("placedAt").GetDateTimeOffset().ShouldBe(placed);
        listed.GetProperty("lines")[0].GetProperty("name").GetString().ShouldBe("Majica basic - M, Siva");
    }
}
