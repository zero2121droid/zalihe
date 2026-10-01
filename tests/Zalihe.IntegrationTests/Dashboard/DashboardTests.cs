using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Dashboard;

[Collection(ApiCollection.Name)]
public class DashboardTests(ZaliheApiFactory factory)
{
    private static async Task<string> ItemAsync(HttpClient client, string sku, decimal minStock, decimal? purchasePrice, decimal receipt)
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name = $"Artikal {sku}", sku, unit = "kom", minStock, purchasePrice });
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        if (receipt > 0)
        {
            await client.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "receipt", quantity = receipt });
        }

        return id;
    }

    private static async Task<JsonElement> DashboardAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/dashboard");

    [Fact]
    public async Task Get_MixedStock_ReturnsValueCountsAndMostUrgentReorders()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        await ItemAsync(client, "OK", minStock: 10, purchasePrice: 900, receipt: 31);      // 27.900, in stock
        await ItemAsync(client, "LOW-40", minStock: 10, purchasePrice: 100, receipt: 4);   // 400, 40 % of minimum
        await ItemAsync(client, "LOW-25", minStock: 8, purchasePrice: null, receipt: 2);   // no price, 25 % of minimum
        await ItemAsync(client, "OUT", minStock: 5, purchasePrice: 2400, receipt: 0);      // out of stock
        var inactive = await ItemAsync(client, "OFF", minStock: 5, purchasePrice: 1000, receipt: 0);
        await client.PostAsync($"/api/items/{inactive}/deactivate", null);

        // Act
        var dashboard = await DashboardAsync(client);

        // Assert
        dashboard.GetProperty("stockValue").GetDecimal().ShouldBe(28300);
        dashboard.GetProperty("activeItems").GetInt32().ShouldBe(4);
        dashboard.GetProperty("belowMinimum").GetInt32().ShouldBe(2);
        dashboard.GetProperty("outOfStock").GetInt32().ShouldBe(1);
        dashboard.GetProperty("reorder").EnumerateArray()
            .Select(r => (r.GetProperty("sku").GetString(), r.GetProperty("status").GetString()))
            .ShouldBe([("OUT", "outOfStock"), ("LOW-25", "low"), ("LOW-40", "low")]);
    }

    [Fact]
    public async Task Get_SeveralMovements_ReturnsLatestFirstWithItemName()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var id = await ItemAsync(client, "KF-1", minStock: 1, purchasePrice: null, receipt: 24);
        await client.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "sale", quantity = 2, note = "Radnja" });

        // Act
        var recent = (await DashboardAsync(client)).GetProperty("recentMovements").EnumerateArray().ToList();

        // Assert
        recent.Select(m => m.GetProperty("quantity").GetDecimal()).ShouldBe([-2m, 24m]);
        recent[0].GetProperty("itemName").GetString().ShouldBe("Artikal KF-1");
        recent[0].GetProperty("type").GetString().ShouldBe("sale");
        recent[0].GetProperty("note").GetString().ShouldBe("Radnja");
    }

    [Fact]
    public async Task Get_NewCompany_IsEmptyAndIgnoresOtherCompanies()
    {
        // Arrange
        var other = await factory.CreateSignedInClientAsync();
        await ItemAsync(other, "OTHER", minStock: 10, purchasePrice: 100, receipt: 1);
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var dashboard = await DashboardAsync(client);

        // Assert
        dashboard.GetProperty("stockValue").GetDecimal().ShouldBe(0);
        dashboard.GetProperty("activeItems").GetInt32().ShouldBe(0);
        dashboard.GetProperty("reorder").GetArrayLength().ShouldBe(0);
        dashboard.GetProperty("recentMovements").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        (await factory.CreateClient().GetAsync("/api/dashboard")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
