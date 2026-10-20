using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Items;

[Collection(ApiCollection.Name)]
public class ItemSortTests(ZaliheApiFactory factory)
{
    /// <summary>A company with four items: different stock, sales and value, one without a purchase price.</summary>
    private async Task<HttpClient> ShopAsync()
    {
        var client = await factory.CreateSignedInClientAsync();
        await ItemAsync(client, "B-SOLJA", "Šolja", receipt: 10, sold: 1, price: 300);   // value 2.700
        await ItemAsync(client, "A-KAFA", "Kafa", receipt: 40, sold: 25, price: 900);    // value 13.500
        await ItemAsync(client, "C-MLIN", "Mlin", receipt: 3, sold: 0, price: null);     // no value
        await ItemAsync(client, "D-FILTER", "Filter", receipt: 60, sold: 8, price: 50);  // value 2.600
        return client;
    }

    private static async Task ItemAsync(HttpClient client, string sku, string name, decimal receipt, decimal sold, decimal? price)
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name, sku, unit = "kom", minStock = 0, purchasePrice = price });
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "receipt", quantity = receipt })).EnsureSuccessStatusCode();
        if (sold > 0)
        {
            (await client.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "sale", quantity = sold })).EnsureSuccessStatusCode();
        }
    }

    private static async Task<string?[]> SkusAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/items{query}"))
            .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()).ToArray();

    [Theory]
    [InlineData("", new[] { "D-FILTER", "A-KAFA", "C-MLIN", "B-SOLJA" })]
    [InlineData("?sort=name&direction=desc", new[] { "B-SOLJA", "C-MLIN", "A-KAFA", "D-FILTER" })]
    [InlineData("?sort=stock&direction=asc", new[] { "C-MLIN", "B-SOLJA", "A-KAFA", "D-FILTER" })]
    [InlineData("?sort=stock&direction=desc", new[] { "D-FILTER", "A-KAFA", "B-SOLJA", "C-MLIN" })]
    [InlineData("?sort=sold30Days&direction=desc", new[] { "A-KAFA", "D-FILTER", "B-SOLJA", "C-MLIN" })]
    public async Task List_SortedByColumn_ReturnsItemsInThatOrder(string query, string[] expected)
    {
        // Arrange
        var client = await ShopAsync();

        // Act
        var skus = await SkusAsync(client, query);

        // Assert
        skus.ShouldBe(expected);
    }

    [Theory]
    [InlineData("desc", new[] { "A-KAFA", "B-SOLJA", "D-FILTER", "C-MLIN" })]
    [InlineData("asc", new[] { "D-FILTER", "B-SOLJA", "A-KAFA", "C-MLIN" })]
    public async Task List_SortedByValue_ItemsWithoutPurchasePriceComeLastEitherWay(string direction, string[] expected)
    {
        // Arrange
        var client = await ShopAsync();

        // Act
        var skus = await SkusAsync(client, $"?sort=value&direction={direction}");

        // Assert
        skus.ShouldBe(expected);
    }

    [Fact]
    public async Task List_SortedWithEqualValues_PagesDontRepeatOrSkipItems()
    {
        // Arrange: five items with the same stock (0), so only the name decides.
        var client = await factory.CreateSignedInClientAsync();
        foreach (var letter in new[] { "C", "A", "E", "B", "D" })
        {
            await client.PostAsJsonAsync("/api/items", new { name = $"Artikal {letter}", sku = $"SKU-{letter}", unit = "kom", minStock = 0 });
        }

        // Act
        var first = await SkusAsync(client, "?sort=stock&direction=desc&page=1&pageSize=2");
        var second = await SkusAsync(client, "?sort=stock&direction=desc&page=2&pageSize=2");
        var third = await SkusAsync(client, "?sort=stock&direction=desc&page=3&pageSize=2");

        // Assert
        first.Concat(second).Concat(third).ShouldBe(["SKU-A", "SKU-B", "SKU-C", "SKU-D", "SKU-E"]);
    }

    [Fact]
    public async Task List_UnknownSort_ReturnsValidationError()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.GetAsync("/api/items?sort=purchasePrice");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("validation.invalid");
        error.GetProperty("field").GetString().ShouldBe("sort");
    }
}
