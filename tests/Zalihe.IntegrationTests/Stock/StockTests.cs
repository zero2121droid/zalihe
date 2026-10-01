using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Stock;

[Collection(ApiCollection.Name)]
public class StockTests(ZaliheApiFactory factory)
{
    private static async Task<string> CreateItemAsync(HttpClient client, string sku = "KF-ETI-250", decimal minStock = 10, decimal? purchasePrice = null)
    {
        var response = await client.PostAsJsonAsync("/api/items",
            new { name = $"Artikal {sku}", sku, unit = "kom", minStock, purchasePrice });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> RecordAsync(HttpClient client, string itemId, string kind, decimal quantity, string? note = null) =>
        client.PostAsJsonAsync($"/api/items/{itemId}/movements", new { kind, quantity, note });

    private static async Task<JsonElement> GetItemAsync(HttpClient client, string itemId) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}");

    [Fact]
    public async Task CreateItem_New_StartsWithZeroStockAndOutOfStock()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var item = await GetItemAsync(client, await CreateItemAsync(client));

        // Assert
        item.GetProperty("stock").GetDecimal().ShouldBe(0);
        item.GetProperty("status").GetString().ShouldBe("outOfStock");
    }

    [Fact]
    public async Task Record_Receipt_IncreasesStockAndReturnsMovement()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        var response = await RecordAsync(client, itemId, "receipt", 24, "Dobavljač");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("stock").GetDecimal().ShouldBe(24);
        var movement = body.GetProperty("movement");
        movement.GetProperty("type").GetString().ShouldBe("receipt");
        movement.GetProperty("quantity").GetDecimal().ShouldBe(24);
        movement.GetProperty("source").GetString().ShouldBe("manual");
        movement.GetProperty("note").GetString().ShouldBe("Dobavljač");
        movement.GetProperty("userName").GetString().ShouldBe("Miljan");
        (await GetItemAsync(client, itemId)).GetProperty("status").GetString().ShouldBe("inStock");
    }

    [Fact]
    public async Task Record_SaleBeyondStock_IsAllowedAndGoesNegative()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);
        await RecordAsync(client, itemId, "receipt", 1);

        // Act
        var response = await RecordAsync(client, itemId, "sale", 3);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("movement").GetProperty("quantity").GetDecimal().ShouldBe(-3);
        body.GetProperty("stock").GetDecimal().ShouldBe(-2);
        (await GetItemAsync(client, itemId)).GetProperty("status").GetString().ShouldBe("outOfStock");
    }

    [Fact]
    public async Task Record_Count_RecordsDifferenceAndSetsStockToCounted()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);
        await RecordAsync(client, itemId, "receipt", 30);

        // Act
        var response = await RecordAsync(client, itemId, "count", 27.5m, "Popis");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("movement").GetProperty("type").GetString().ShouldBe("adjustment");
        body.GetProperty("movement").GetProperty("quantity").GetDecimal().ShouldBe(-2.5m);
        body.GetProperty("stock").GetDecimal().ShouldBe(27.5m);
    }

    [Fact]
    public async Task Record_CountEqualToStock_ReturnsNoChange()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);
        await RecordAsync(client, itemId, "receipt", 5);

        // Act
        var response = await RecordAsync(client, itemId, "count", 5, "Popis");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("stock.no_change");
    }

    [Fact]
    public async Task Record_CountWithoutNote_RequiresNote()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        var response = await RecordAsync(client, itemId, "count", 3);

        // Assert
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("validation.required");
        error.GetProperty("field").GetString().ShouldBe("note");
    }

    [Theory]
    [InlineData("receipt", 0, "validation.positive")]
    [InlineData("sale", -2, "validation.positive")]
    [InlineData("receipt", 1.2345, "validation.too_many_decimals")]
    public async Task Record_InvalidQuantity_ReturnsErrorForQuantity(string kind, decimal quantity, string code)
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        var response = await RecordAsync(client, itemId, kind, quantity);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe(code);
        error.GetProperty("field").GetString().ShouldBe("quantity");
    }

    [Fact]
    public async Task Record_TenConcurrentReceipts_NoneIsLost()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => RecordAsync(client, itemId, "receipt", 1)));

        // Assert
        var failures = await Task.WhenAll(responses.Where(r => r.StatusCode != HttpStatusCode.Created)
            .Select(async r => $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}"));
        failures.ShouldBeEmpty();
        (await GetItemAsync(client, itemId)).GetProperty("stock").GetDecimal().ShouldBe(10);
    }

    [Fact]
    public async Task StockLevel_AfterManyMovements_EqualsSumOfMovements()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        await RecordAsync(client, itemId, "receipt", 24);
        await RecordAsync(client, itemId, "sale", 2.5m);
        await RecordAsync(client, itemId, "return", 1);
        await RecordAsync(client, itemId, "count", 20, "Popis");
        await RecordAsync(client, itemId, "sale", 21);

        // Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.Parse(itemId);
        var sum = await db.StockMovements.IgnoreQueryFilters().Where(m => m.ItemId == id).SumAsync(m => m.Quantity);
        var level = await db.StockLevels.IgnoreQueryFilters().SingleAsync(l => l.ItemId == id);
        level.Quantity.ShouldBe(sum);
        level.Quantity.ShouldBe(-1);
    }

    [Fact]
    public async Task History_SeveralMovements_NewestFirstWithPaging()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);
        await RecordAsync(client, itemId, "receipt", 10);
        await RecordAsync(client, itemId, "sale", 2);
        await RecordAsync(client, itemId, "sale", 3);

        // Act
        var page1 = await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}/history?filter=stock&pageSize=2");
        var page2 = await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}/history?filter=stock&pageSize=2&page=2");

        // Assert
        page1.GetProperty("totalCount").GetInt32().ShouldBe(3);
        page1.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("movement").GetProperty("quantity").GetDecimal()).ShouldBe([-3m, -2m]);
        page2.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("movement").GetProperty("quantity").GetDecimal()).ShouldBe([10m]);
    }

    [Fact]
    public async Task RecordAndHistory_ItemOfOtherCompany_Return404AndChangeNothing()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(companyA);
        await RecordAsync(companyA, itemId, "receipt", 5);

        // Act
        var record = await RecordAsync(companyB, itemId, "sale", 5);
        var history = await companyB.GetAsync($"/api/items/{itemId}/history");

        // Assert
        record.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        history.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetItemAsync(companyA, itemId)).GetProperty("stock").GetDecimal().ShouldBe(5);
    }

    [Fact]
    public async Task ListAndSummary_MixedStock_FilterByStatusAndCount()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var ok = await CreateItemAsync(client, "OK-1", minStock: 10);
        var low = await CreateItemAsync(client, "LOW-1", minStock: 10);
        await CreateItemAsync(client, "OUT-1", minStock: 10);
        await RecordAsync(client, ok, "receipt", 31);
        await RecordAsync(client, low, "receipt", 10);

        // Act
        async Task<string[]> Skus(string status) =>
            (await client.GetFromJsonAsync<JsonElement>($"/api/items?status={status}")).GetProperty("items").EnumerateArray()
                .Select(i => i.GetProperty("sku").GetString()!).ToArray();
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/stock/summary");

        // Assert
        (await Skus("inStock")).ShouldBe(["OK-1"]);
        (await Skus("low")).ShouldBe(["LOW-1"]);
        (await Skus("outOfStock")).ShouldBe(["OUT-1"]);
        summary.GetProperty("belowMinimum").GetInt32().ShouldBe(1);
        summary.GetProperty("outOfStock").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task List_ItemWithSalesAndPrice_ShowsSold30DaysAndValue()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client, purchasePrice: 900.5m);
        await RecordAsync(client, itemId, "receipt", 10);
        await RecordAsync(client, itemId, "sale", 2);
        await RecordAsync(client, itemId, "sale", 1.5m);
        await RecordAsync(client, itemId, "return", 1);

        // Act
        var item = (await client.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("items")[0];

        // Assert
        item.GetProperty("stock").GetDecimal().ShouldBe(7.5m);
        item.GetProperty("sold30Days").GetDecimal().ShouldBe(3.5m);
        item.GetProperty("stockValue").GetDecimal().ShouldBe(6753.75m);
    }
}
