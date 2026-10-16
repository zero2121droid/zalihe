using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Zalihe.IntegrationTests.Infrastructure;

public static class LinkedShop
{
    /// <summary>
    /// A signed-in company with a connected shop whose two products are imported and linked:
    /// coffee (product 10, SKU KF-ETI-250, 24 in stock) and a T-shirt variation (51 of product 50,
    /// SKU MAJ-M-SI, 5 in stock).
    /// </summary>
    public static async Task<(HttpClient Client, Guid ChannelId)> CreateAsync(ZaliheApiFactory factory)
    {
        var client = await factory.CreateSignedInClientAsync();
        factory.Shop.RespondWith(_ => FakeShopHandler.Json("[]"));
        var connect = await client.PostAsJsonAsync("/api/channels/woocommerce",
            new { baseUrl = "zrno.rs", consumerKey = "ck_1234567890abcdef", consumerSecret = "cs_1234567890abcdef" });
        connect.StatusCode.ShouldBe(HttpStatusCode.Created);
        var channelId = (await connect.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

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
        var import = await client.PostAsJsonAsync($"/api/channels/{channelId}/products/import", new { createExternalIds = new[] { "10", "51" } });
        import.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (client, channelId);
    }

    public static async Task<JsonElement> ItemAsync(HttpClient client, string sku)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/items?search={sku}");
        return page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("sku").GetString() == sku);
    }

    public static async Task<decimal> StockAsync(HttpClient client, string sku) =>
        (await ItemAsync(client, sku)).GetProperty("stock").GetDecimal();

    /// <summary>The item's stock movements that came from shop orders, newest first.</summary>
    public static async Task<List<JsonElement>> OrderMovementsAsync(HttpClient client, string sku)
    {
        var id = (await ItemAsync(client, sku)).GetProperty("id").GetString();
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/items/{id}/history?filter=stock");
        return history.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("movement"))
            .Where(m => m.GetProperty("externalRef").ValueKind == JsonValueKind.String)
            .ToList();
    }
}
