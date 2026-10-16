using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Application.Channels;
using Zalihe.Application.Orders;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Infrastructure.Tenancy;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Orders;

/// <summary>
/// Orders applied to stock against the real database. Orders don't arrive over HTTP yet
/// (webhooks come next), so the service is called the way a background job will call it:
/// in its own scope, with the company set explicitly.
/// </summary>
[Collection(ApiCollection.Name)]
public class OrderProcessingTests(ZaliheApiFactory factory)
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddMinutes(5);

    /// <summary>A shop with coffee (product 10, 24 in stock) and a T-shirt variation (51 of product 50, 5 in stock).</summary>
    private void ShopHasProducts() =>
        factory.Shop.RespondWith(request =>
        {
            var url = request.RequestUri!.ToString();
            if (!url.EndsWith("&page=1", StringComparison.Ordinal)) return FakeShopHandler.Json("[]");
            return FakeShopHandler.Json(url.Contains("/variations", StringComparison.Ordinal)
                ? """[{"id":51,"sku":"MAJ-M-SI","regular_price":"1990","stock_quantity":5,"attributes":[{"name":"Veličina","option":"M"}]}]"""
                : """
                  [
                    {"id":10,"name":"Kafa Etiopija","type":"simple","sku":"KF-ETI-250","regular_price":"1250","stock_quantity":24,"categories":[]},
                    {"id":50,"name":"Majica basic","type":"variable","sku":"MAJ-BASIC","regular_price":"","stock_quantity":null,"categories":[]}
                  ]
                  """);
        });

    /// <summary>A company with a connected shop whose two products are imported and linked.</summary>
    private async Task<(HttpClient Client, Guid ChannelId)> ShopWithLinkedProductsAsync()
    {
        var client = await factory.CreateSignedInClientAsync();
        factory.Shop.RespondWith(_ => FakeShopHandler.Json("[]"));
        var connect = await client.PostAsJsonAsync("/api/channels/woocommerce",
            new { baseUrl = "zrno.rs", consumerKey = "ck_1234567890abcdef", consumerSecret = "cs_1234567890abcdef" });
        connect.StatusCode.ShouldBe(HttpStatusCode.Created);
        var channelId = (await connect.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        ShopHasProducts();
        var import = await client.PostAsJsonAsync($"/api/channels/{channelId}/products/import", new { createExternalIds = new[] { "10", "51" } });
        import.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (client, channelId);
    }

    private async Task<ProcessOrderResult> ProcessAsync(Guid channelId, ShopOrder order, Guid? asTenant = null)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var tenantId = asTenant ?? await services.GetRequiredService<AppDbContext>().SalesChannels
            .IgnoreQueryFilters().Where(c => c.Id == channelId).Select(c => c.TenantId).SingleAsync();
        services.GetRequiredService<TenantContext>().Set(tenantId);
        return await services.GetRequiredService<OrderService>().ProcessAsync(channelId, order, CancellationToken.None);
    }

    private static ShopOrder Order(string status, DateTimeOffset modifiedAt, params (string ProductId, decimal Quantity)[] lines) =>
        new("1041", status, modifiedAt, lines.Select(l => new ShopOrderLine(l.ProductId, l.Quantity)).ToList());

    private static async Task<JsonElement> ItemAsync(HttpClient client, string sku)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/items?search={sku}");
        return page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("sku").GetString() == sku);
    }

    private static async Task<decimal> StockAsync(HttpClient client, string sku) =>
        (await ItemAsync(client, sku)).GetProperty("stock").GetDecimal();

    private static async Task<List<JsonElement>> OrderMovementsAsync(HttpClient client, string sku)
    {
        var id = (await ItemAsync(client, sku)).GetProperty("id").GetString();
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/items/{id}/history?filter=stock");
        return history.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("movement"))
            .Where(m => m.GetProperty("externalRef").ValueKind == JsonValueKind.String)
            .ToList();
    }

    [Fact]
    public async Task Process_NewOrder_RecordsSalesAndLowersStock()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();

        // Act
        var result = await ProcessAsync(channelId, Order("processing", T1, ("10", 2), ("51", 1)));

        // Assert
        result.ShouldBe(new ProcessOrderResult(Ignored: false, MovementCount: 2, UnlinkedLineCount: 0));
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(22);
        (await StockAsync(client, "MAJ-M-SI")).ShouldBe(4);

        var sale = (await OrderMovementsAsync(client, "KF-ETI-250")).ShouldHaveSingleItem();
        sale.GetProperty("type").GetString().ShouldBe("sale");
        sale.GetProperty("source").GetString().ShouldBe("wooCommerce");
        sale.GetProperty("quantity").GetDecimal().ShouldBe(-2);
        sale.GetProperty("externalRef").GetString().ShouldBe("1041");
        sale.GetProperty("occurredAt").GetDateTimeOffset().ShouldBe(T1);
        sale.GetProperty("userName").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Process_SameEventTwice_RecordsItOnce()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();
        await ProcessAsync(channelId, Order("processing", T1, ("10", 2)));

        // Act
        var result = await ProcessAsync(channelId, Order("processing", T1, ("10", 2)));

        // Assert
        result.MovementCount.ShouldBe(0);
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(22);
        (await OrderMovementsAsync(client, "KF-ETI-250")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Process_CancelledOrder_ReturnsStockWithAReturnMovement()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();
        await ProcessAsync(channelId, Order("processing", T1, ("10", 2)));

        // Act
        await ProcessAsync(channelId, Order("cancelled", T2, ("10", 2)));

        // Assert
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(24);
        var movements = await OrderMovementsAsync(client, "KF-ETI-250");
        movements.Select(m => m.GetProperty("type").GetString()).ShouldBe(["return", "sale"]);
        movements[0].GetProperty("quantity").GetDecimal().ShouldBe(2);
    }

    [Fact]
    public async Task Process_LineAddedAndQuantityChanged_RecordsOnlyTheDifferences()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();
        await ProcessAsync(channelId, Order("processing", T1, ("10", 2)));

        // Act: coffee goes from 2 to 3, and a T-shirt is added to the stored order
        var result = await ProcessAsync(channelId, Order("processing", T2, ("10", 3), ("51", 1)));

        // Assert
        result.MovementCount.ShouldBe(2);
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(21);
        (await StockAsync(client, "MAJ-M-SI")).ShouldBe(4);
        (await OrderMovementsAsync(client, "KF-ETI-250"))[0].GetProperty("quantity").GetDecimal().ShouldBe(-1);
    }

    [Fact]
    public async Task Process_OlderEventAfterNewer_IsIgnored()
    {
        // Arrange: the cancellation arrives first
        var (client, channelId) = await ShopWithLinkedProductsAsync();
        await ProcessAsync(channelId, Order("cancelled", T2, ("10", 2)));

        // Act
        var result = await ProcessAsync(channelId, Order("processing", T1, ("10", 2)));

        // Assert
        result.Ignored.ShouldBeTrue();
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(24);
        (await OrderMovementsAsync(client, "KF-ETI-250")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Process_UnlinkedProduct_IsKeptWithoutTakingStock()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();

        // Act
        var result = await ProcessAsync(channelId, Order("processing", T1, ("10", 1), ("99", 4)));

        // Assert
        result.ShouldBe(new ProcessOrderResult(Ignored: false, MovementCount: 1, UnlinkedLineCount: 1));
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(23);

        using var scope = factory.Services.CreateScope();
        var lines = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ExternalOrders
            .IgnoreQueryFilters().Where(o => o.ChannelId == channelId).SelectMany(o => o.Lines).ToListAsync();
        var unlinked = lines.Single(l => l.ExternalProductId == "99");
        unlinked.ItemId.ShouldBeNull();
        unlinked.OrderedQuantity.ShouldBe(4);
    }

    [Fact]
    public async Task Process_ManyEvents_StockEqualsSumOfMovements()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();
        var events = new[]
        {
            Order("pending", T1, ("10", 2)),
            Order("processing", T1.AddMinutes(1), ("10", 2), ("51", 2)),
            Order("processing", T1.AddMinutes(2), ("10", 5)),
            Order("cancelled", T1.AddMinutes(3), ("10", 5)),
            Order("processing", T1.AddMinutes(4), ("10", 1), ("51", 1)),
        };

        // Act
        foreach (var e in events) await ProcessAsync(channelId, e);

        // Assert
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(23);
        (await StockAsync(client, "MAJ-M-SI")).ShouldBe(4);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.SalesChannels.IgnoreQueryFilters().Where(c => c.Id == channelId).Select(c => c.TenantId).SingleAsync();
        var levels = await db.StockLevels.IgnoreQueryFilters().Where(l => l.TenantId == tenantId).ToListAsync();
        foreach (var level in levels)
        {
            var sum = await db.StockMovements.IgnoreQueryFilters().Where(m => m.ItemId == level.ItemId).SumAsync(m => m.Quantity);
            level.Quantity.ShouldBe(sum);
        }
    }

    [Fact]
    public async Task Process_ConcurrentEventsForStoredOrder_AppliesTheDifferenceOnce()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();
        await ProcessAsync(channelId, Order("processing", T1, ("10", 2)));

        // Act: the same change (2 → 5) arrives five times at once
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ProcessAsync(channelId, Order("processing", T2, ("10", 5)))));

        // Assert
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(19);
        (await OrderMovementsAsync(client, "KF-ETI-250")).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Process_ConcurrentEventsForNewOrder_OneFailsAndItsRetryChangesNothing()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();
        var order = Order("processing", T1, ("10", 2));

        // Act: two events for an order that isn't stored yet; a failed one is retried, as a job would be
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await ProcessAsync(channelId, order);
            }
            catch (DbUpdateException)
            {
                await ProcessAsync(channelId, order);
            }
        }));

        // Assert
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(22);
        (await OrderMovementsAsync(client, "KF-ETI-250")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Process_ShopOfAnotherCompany_IsRejectedAndChangesNothing()
    {
        // Arrange
        var (client, channelId) = await ShopWithLinkedProductsAsync();
        var (_, otherChannelId) = await ShopWithLinkedProductsAsync();
        using var scope = factory.Services.CreateScope();
        var otherTenant = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SalesChannels
            .IgnoreQueryFilters().Where(c => c.Id == otherChannelId).Select(c => c.TenantId).SingleAsync();

        // Act
        var act = () => ProcessAsync(channelId, Order("processing", T1, ("10", 2)), asTenant: otherTenant);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
        (await StockAsync(client, "KF-ETI-250")).ShouldBe(24);
    }
}
