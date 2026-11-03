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

/// <summary>Manual orders (SPEC 11.5): POST /api/orders and PUT /api/orders/{id}/status.</summary>
[Collection(ApiCollection.Name)]
public class ManualOrdersTests(ZaliheApiFactory factory)
{
    private static async Task<string> CreateItemAsync(HttpClient client, string sku, decimal stock)
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name = $"Artikal {sku}", sku, unit = "kom", minStock = 0 });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        if (stock > 0)
        {
            (await client.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "receipt", quantity = stock }))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        return id;
    }

    private static Task<HttpResponseMessage> CreateOrderAsync(HttpClient client, object[] lines, string? reference = null, string? note = null) =>
        client.PostAsJsonAsync("/api/orders", new { lines, reference, note });

    private static async Task<JsonElement> CreatedOrderAsync(HttpClient client, params object[] lines)
    {
        var response = await CreateOrderAsync(client, lines);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> SetStatusAsync(HttpClient client, string orderId, string status) =>
        client.PutAsJsonAsync($"/api/orders/{orderId}/status", new { status });

    private static async Task<decimal> StockAsync(HttpClient client, string itemId) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}")).GetProperty("stock").GetDecimal();

    private static async Task<List<JsonElement>> MovementsAsync(HttpClient client, string itemId)
    {
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}/history?filter=stock");
        return history.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("movement")).ToList();
    }

    [Fact]
    public async Task Create_TwoItems_TakesStockAndReturnsConfirmedOrder()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var coffee = await CreateItemAsync(client, "KF-1", 10);
        var shirt = await CreateItemAsync(client, "MAJ-1", 3);

        // Act
        var response = await CreateOrderAsync(client,
            [new { itemId = coffee, quantity = 2.5m }, new { itemId = shirt, quantity = 1 }], " IG 154 ", "Lično preuzimanje");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        order.GetProperty("number").GetString().ShouldBe("R-0001");
        order.GetProperty("source").GetString().ShouldBe("manual");
        order.GetProperty("channelId").ValueKind.ShouldBe(JsonValueKind.Null);
        order.GetProperty("shopUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        order.GetProperty("reference").GetString().ShouldBe("IG 154");
        order.GetProperty("note").GetString().ShouldBe("Lično preuzimanje");
        order.GetProperty("status").GetString().ShouldBe("confirmed");
        order.GetProperty("takesStock").GetBoolean().ShouldBeTrue();
        order.GetProperty("unlinkedLineCount").GetInt32().ShouldBe(0);
        var lines = order.GetProperty("lines").EnumerateArray().ToList();
        lines.Select(l => l.GetProperty("state").GetString()).ShouldBe(["linked", "linked"]);
        lines.Select(l => l.GetProperty("item").GetProperty("sku").GetString()).ShouldBe(["KF-1", "MAJ-1"], ignoreOrder: true);

        (await StockAsync(client, coffee)).ShouldBe(7.5m);
        (await StockAsync(client, shirt)).ShouldBe(2);
        var sale = (await MovementsAsync(client, coffee))[0];
        sale.GetProperty("type").GetString().ShouldBe("sale");
        sale.GetProperty("quantity").GetDecimal().ShouldBe(-2.5m);
        sale.GetProperty("source").GetString().ShouldBe("manual");
        sale.GetProperty("externalRef").GetString().ShouldBe("R-0001");
        sale.GetProperty("userName").GetString().ShouldBe("Miljan");
    }

    [Fact]
    public async Task Create_SeveralOrders_NumbersFollowEachOtherPerCompany()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var other = await factory.CreateSignedInClientAsync();
        var item = await CreateItemAsync(client, "KF-1", 10);
        var otherItem = await CreateItemAsync(other, "KF-1", 10);
        await CreatedOrderAsync(client, new { itemId = item, quantity = 1 });

        // Act
        var second = await CreatedOrderAsync(client, new { itemId = item, quantity = 1 });
        var othersFirst = await CreatedOrderAsync(other, new { itemId = otherItem, quantity = 1 });

        // Assert
        second.GetProperty("number").GetString().ShouldBe("R-0002");
        othersFirst.GetProperty("number").GetString().ShouldBe("R-0001");
    }

    [Fact]
    public async Task Create_AtTheSameTime_GetsDifferentNumbers()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var item = await CreateItemAsync(client, "KF-1", 10);

        // Act
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CreateOrderAsync(client, [new { itemId = item, quantity = 1 }])));

        // Assert
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        var numbers = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("number").GetString()));
        numbers.Order().ShouldBe(["R-0001", "R-0002", "R-0003", "R-0004", "R-0005"]);
        (await StockAsync(client, item)).ShouldBe(5);
    }

    [Fact]
    public async Task Create_BeyondStock_IsAllowedAndGoesNegative()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var item = await CreateItemAsync(client, "KF-1", 1);

        // Act
        await CreatedOrderAsync(client, new { itemId = item, quantity = 3 });

        // Assert
        (await StockAsync(client, item)).ShouldBe(-2);
    }

    [Fact]
    public async Task Create_NoLines_ReturnsValidationError()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await CreateOrderAsync(client, []);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("validation.required");
        problem.GetProperty("errors")[0].GetProperty("field").GetString().ShouldBe("lines");
    }

    [Fact]
    public async Task Create_ZeroQuantity_ReturnsValidationErrorAndTakesNothing()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var coffee = await CreateItemAsync(client, "KF-1", 10);

        // Act
        var response = await CreateOrderAsync(client, [new { itemId = coffee, quantity = 1 }, new { itemId = coffee, quantity = 0 }]);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("validation.positive");
        error.GetProperty("field").GetString().ShouldBe("lines[1].quantity");
        (await StockAsync(client, coffee)).ShouldBe(10);
    }

    [Fact]
    public async Task Create_OtherCompanysItem_IsRejectedAndUsesNoNumber()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var other = await factory.CreateSignedInClientAsync();
        var mine = await CreateItemAsync(client, "KF-1", 10);
        var theirs = await CreateItemAsync(other, "KF-2", 10);

        // Act
        var response = await CreateOrderAsync(client, [new { itemId = mine, quantity = 1 }, new { itemId = theirs, quantity = 1 }]);
        var next = await CreatedOrderAsync(client, new { itemId = mine, quantity = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("order.item_not_found");
        error.GetProperty("field").GetString().ShouldBe("lines[1].itemId");
        (await StockAsync(other, theirs)).ShouldBe(10);
        next.GetProperty("number").GetString().ShouldBe("R-0001");
    }

    [Fact]
    public async Task Create_AsEmployee_IsAllowed()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var item = await CreateItemAsync(owner, "KF-1", 10);
        var employee = await factory.CreateInvitedClientAsync(owner, "employee");

        // Act
        var response = await CreateOrderAsync(employee, [new { itemId = item, quantity = 1 }]);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await MovementsAsync(owner, item))[0].GetProperty("userName").GetString().ShouldBe("Ana");
    }

    [Fact]
    public async Task SetStatus_Cancelled_ReturnsStockAndConfirmingAgainTakesIt()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var item = await CreateItemAsync(client, "KF-1", 10);
        var orderId = (await CreatedOrderAsync(client, new { itemId = item, quantity = 4 })).GetProperty("id").GetString()!;

        // Act
        var cancel = await SetStatusAsync(client, orderId, "cancelled");
        var stockAfterCancel = await StockAsync(client, item);
        var cancelAgain = await SetStatusAsync(client, orderId, "cancelled");
        var confirm = await SetStatusAsync(client, orderId, "confirmed");

        // Assert
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelled = await cancel.Content.ReadFromJsonAsync<JsonElement>();
        cancelled.GetProperty("status").GetString().ShouldBe("cancelled");
        cancelled.GetProperty("takesStock").GetBoolean().ShouldBeFalse();
        stockAfterCancel.ShouldBe(10);
        cancelAgain.StatusCode.ShouldBe(HttpStatusCode.OK);
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StockAsync(client, item)).ShouldBe(6);

        var movements = await MovementsAsync(client, item);
        movements.Take(3).Select(m => m.GetProperty("type").GetString()).ShouldBe(["sale", "return", "sale"]);
        movements.Take(3).ShouldAllBe(m => m.GetProperty("externalRef").GetString() == "R-0001");
    }

    [Fact]
    public async Task SetStatus_OtherCompanysOrder_ReturnsNotFound()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var other = await factory.CreateSignedInClientAsync();
        var item = await CreateItemAsync(other, "KF-1", 10);
        var orderId = (await CreatedOrderAsync(other, new { itemId = item, quantity = 4 })).GetProperty("id").GetString()!;

        // Act
        var response = await SetStatusAsync(client, orderId, "cancelled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StockAsync(other, item)).ShouldBe(6);
    }

    [Fact]
    public async Task SetStatus_ShopOrder_ReturnsConflict()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var tenantId = await services.GetRequiredService<AppDbContext>().FindChannelTenantAsync(channelId, CancellationToken.None);
            services.GetRequiredService<TenantContext>().Set(tenantId!.Value);
            await services.GetRequiredService<OrderService>().ProcessAsync(channelId,
                new ShopOrder("1041", "processing", DateTimeOffset.UtcNow.AddMinutes(1), DateTimeOffset.UtcNow, [new ShopOrderLine("10", 2)]),
                CancellationToken.None);
        }

        var orderId = (await client.GetFromJsonAsync<JsonElement>("/api/orders")).GetProperty("items")[0].GetProperty("id").GetString()!;

        // Act
        var response = await SetStatusAsync(client, orderId, "cancelled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("order.not_manual");
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(22);
    }

    [Fact]
    public async Task List_ManualOrder_IsFoundByNumberAndReference()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var item = await CreateItemAsync(client, "KF-1", 10);
        (await CreateOrderAsync(client, [new { itemId = item, quantity = 1 }], "IG 154")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await CreatedOrderAsync(client, new { itemId = item, quantity = 1 });

        // Act
        var byReference = await client.GetFromJsonAsync<JsonElement>("/api/orders?search=ig 154");
        var byNumber = await client.GetFromJsonAsync<JsonElement>("/api/orders?search=R-0002");

        // Assert
        byReference.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("number").GetString()).ShouldBe(["R-0001"]);
        byNumber.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("number").GetString()).ShouldBe(["R-0002"]);
    }
}
