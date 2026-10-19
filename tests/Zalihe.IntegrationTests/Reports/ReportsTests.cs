using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public class ReportsTests(ZaliheApiFactory factory)
{
    private static async Task<string> ItemAsync(HttpClient client, string sku, decimal? purchasePrice = null,
        decimal receipt = 0, string? category = null, decimal minStock = 0)
    {
        var response = await client.PostAsJsonAsync("/api/items",
            new { name = $"Artikal {sku}", sku, unit = "kom", minStock, purchasePrice, category });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        if (receipt > 0)
        {
            await MovementAsync(client, id, "receipt", receipt);
        }

        return id;
    }

    private static async Task<string> MovementAsync(HttpClient client, string itemId, string kind, decimal quantity)
    {
        var response = await client.PostAsJsonAsync($"/api/items/{itemId}/movements", new { kind, quantity });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("movement").GetProperty("id").GetString()!;
    }

    /// <summary>The API records everything as happening now; reports need the past, so tests move it there.</summary>
    private async Task ItemAddedDaysAgoAsync(string itemId, int days)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"""UPDATE "Items" SET "CreatedAt" = now() - make_interval(days => {days}) WHERE "Id" = {Guid.Parse(itemId)}""");
    }

    private async Task MovementDaysAgoAsync(string movementId, int days)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"""UPDATE "StockMovements" SET "OccurredAt" = now() - make_interval(days => {days}) WHERE "Id" = {Guid.Parse(movementId)}""");
    }

    private static async Task<List<JsonElement>> ListAsync(HttpClient client, string url) =>
        (await client.GetFromJsonAsync<JsonElement>(url)).GetProperty("items").EnumerateArray().ToList();

    private static IEnumerable<string?> Skus(IEnumerable<JsonElement> rows) => rows.Select(r => r.GetProperty("sku").GetString());

    [Fact]
    public async Task TopSellers_SalesReturnsAndOldSales_RanksByNetSoldInLast30Days()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var coffee = await ItemAsync(client, "KAFA", receipt: 100);
        await MovementAsync(client, coffee, "sale", 10);
        await MovementAsync(client, coffee, "return", 2);              // 8 net
        var tea = await ItemAsync(client, "CAJ", receipt: 100);
        await MovementAsync(client, tea, "sale", 12);
        await MovementDaysAgoAsync(await MovementAsync(client, tea, "sale", 50), 31); // too old
        var cancelled = await ItemAsync(client, "OTKAZANO", receipt: 10);
        await MovementAsync(client, cancelled, "sale", 3);
        await MovementAsync(client, cancelled, "return", 3);           // 0 net: not a seller
        await ItemAsync(client, "NIJE-PRODATO", receipt: 10);

        // Act
        var rows = await ListAsync(client, "/api/reports/top-sellers");

        // Assert
        Skus(rows).ShouldBe(["CAJ", "KAFA"]);
        rows[0].GetProperty("sold").GetDecimal().ShouldBe(12);
        rows[0].GetProperty("stock").GetDecimal().ShouldBe(38);
        rows[1].GetProperty("sold").GetDecimal().ShouldBe(8);
    }

    [Fact]
    public async Task TopSellers_SearchCategoryAndInactive_KeepsOnlyMatchingActiveItems()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        foreach (var (sku, category) in new[] { ("KAFA-1", "Kafa"), ("KAFA-2", "Kafa"), ("CAJ-1", "Čaj") })
        {
            await MovementAsync(client, await ItemAsync(client, sku, receipt: 5, category: category), "sale", 1);
        }

        var inactive = await ItemAsync(client, "KAFA-3", receipt: 5, category: "Kafa");
        await MovementAsync(client, inactive, "sale", 1);
        await client.PostAsync($"/api/items/{inactive}/deactivate", null);

        // Act
        var kafa = await ListAsync(client, "/api/reports/top-sellers?category=Kafa");
        var searched = await ListAsync(client, "/api/reports/top-sellers?search=caj-");

        // Assert
        Skus(kafa).ShouldBe(["KAFA-1", "KAFA-2"]);
        Skus(searched).ShouldBe(["CAJ-1"]);
    }

    [Fact]
    public async Task SlowMovers_ItemsWithAndWithoutRecentSales_ListsUnsoldOldStockByValue()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var cheap = await ItemAsync(client, "JEFTINO", purchasePrice: 10, receipt: 5);       // 50
        var expensive = await ItemAsync(client, "SKUPO", purchasePrice: 1000, receipt: 2);   // 2.000
        var noPrice = await ItemAsync(client, "BEZ-CENE", receipt: 3);
        var soldLongAgo = await ItemAsync(client, "STARA-PRODAJA", purchasePrice: 100, receipt: 5);
        var oldSale = await MovementAsync(client, soldLongAgo, "sale", 1);                  // 400 left
        var soldRecently = await ItemAsync(client, "PRODAJE-SE", purchasePrice: 100, receipt: 5);
        await MovementAsync(client, soldRecently, "sale", 1);
        var empty = await ItemAsync(client, "PRAZNO", purchasePrice: 100);
        var addedRecently = await ItemAsync(client, "NOVO", purchasePrice: 100, receipt: 5);
        foreach (var id in new[] { cheap, expensive, noPrice, soldLongAgo, soldRecently, empty })
        {
            await ItemAddedDaysAgoAsync(id, 90);
        }

        await ItemAddedDaysAgoAsync(addedRecently, 20);
        await MovementDaysAgoAsync(oldSale, 61);

        // Act
        var rows = await ListAsync(client, "/api/reports/slow-movers");
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/reports/summary");

        // Assert
        Skus(rows).ShouldBe(["SKUPO", "STARA-PRODAJA", "JEFTINO", "BEZ-CENE"]);
        rows[0].GetProperty("value").GetDecimal().ShouldBe(2000);
        rows[0].GetProperty("lastSoldAt").ValueKind.ShouldBe(JsonValueKind.Null);
        rows[1].GetProperty("lastSoldAt").GetDateTimeOffset().ShouldBeLessThan(DateTimeOffset.UtcNow.AddDays(-60));
        rows[3].GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);
        summary.GetProperty("slowMoverCount").GetInt32().ShouldBe(4);
        summary.GetProperty("slowMoverValue").GetDecimal().ShouldBe(2450);
    }

    [Fact]
    public async Task Summary_PricedUnpricedAndCategory_ReturnsValueAndItemsWithoutPrice()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        await ItemAsync(client, "A", purchasePrice: 900.5m, receipt: 2, category: "Kafa");   // 1.801
        await ItemAsync(client, "B", purchasePrice: 100, receipt: 3, category: "Čaj");       // 300
        await ItemAsync(client, "C", receipt: 4, category: "Kafa");                          // no price
        await ItemAsync(client, "D", category: "Kafa");                                      // no price, no stock

        // Act
        var all = await client.GetFromJsonAsync<JsonElement>("/api/reports/summary");
        var kafa = await client.GetFromJsonAsync<JsonElement>("/api/reports/summary?category=Kafa");

        // Assert
        all.GetProperty("stockValue").GetDecimal().ShouldBe(2101);
        all.GetProperty("itemsWithoutPrice").GetInt32().ShouldBe(1);
        kafa.GetProperty("stockValue").GetDecimal().ShouldBe(1801);
    }

    [Fact]
    public async Task StockValue_SeveralCategories_GroupsMostValuableFirst()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        await ItemAsync(client, "A", purchasePrice: 100, receipt: 2, category: "Kafa");
        await ItemAsync(client, "B", receipt: 4, category: "Kafa");
        await ItemAsync(client, "C", purchasePrice: 1000, receipt: 1, category: "Čaj");
        await ItemAsync(client, "D", purchasePrice: 50, receipt: 1);

        // Act
        var rows = await ListAsync(client, "/api/reports/stock-value");

        // Assert
        rows.Select(r => (r.GetProperty("category").GetString(), r.GetProperty("value").GetDecimal(),
                r.GetProperty("itemCount").GetInt32(), r.GetProperty("itemsWithoutPrice").GetInt32()))
            .ShouldBe([("Čaj", 1000m, 1, 0), ("Kafa", 200m, 2, 1), (null, 50m, 1, 0)]);
    }

    [Fact]
    public async Task Reports_OtherCompanysItems_AreNotShown()
    {
        // Arrange
        var other = await factory.CreateSignedInClientAsync();
        var item = await ItemAsync(other, "TUDJE", purchasePrice: 100, receipt: 5, category: "Kafa");
        await MovementAsync(other, item, "sale", 1);
        await ItemAddedDaysAgoAsync(await ItemAsync(other, "TUDJE-STARO", purchasePrice: 100, receipt: 5), 90);
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/reports/summary");

        // Assert
        (await ListAsync(client, "/api/reports/top-sellers")).ShouldBeEmpty();
        (await ListAsync(client, "/api/reports/slow-movers")).ShouldBeEmpty();
        (await ListAsync(client, "/api/reports/stock-value")).ShouldBeEmpty();
        summary.GetProperty("stockValue").GetDecimal().ShouldBe(0);
        summary.GetProperty("slowMoverCount").GetInt32().ShouldBe(0);
    }

    [Theory]
    [InlineData("/api/reports/summary")]
    [InlineData("/api/reports/top-sellers")]
    [InlineData("/api/reports/slow-movers")]
    [InlineData("/api/reports/stock-value")]
    public async Task Reports_Anonymous_Returns401(string url)
    {
        (await factory.CreateClient().GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TopSellers_PageSizeTooLarge_Returns400()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.GetAsync("/api/reports/top-sellers?pageSize=1000");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
