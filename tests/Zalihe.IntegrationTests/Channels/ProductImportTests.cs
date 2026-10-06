using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Channels;

[Collection(ApiCollection.Name)]
public partial class ProductImportTests(ZaliheApiFactory factory)
{
    /// <summary>
    /// The shop of these tests: a product that exists in Zalihe (KF-ETI-250), one that doesn't
    /// (WOO-ONLY-1), one without a SKU, a variable T-shirt with two variations and a grouped
    /// product, which holds no stock and is left out.
    /// </summary>
    private const string Products = """
        [
          {"id":10,"name":"Kafa Etiopija","type":"simple","sku":"KF-ETI-250","regular_price":"1250","price":"1250","stock_quantity":24,"categories":[{"id":1,"name":"Kafa","slug":"kafa"}]},
          {"id":11,"name":"Proizvod &amp; poklon","type":"simple","sku":"WOO-ONLY-1","regular_price":"500.50","price":"450","stock_quantity":3,"categories":[{"id":15,"name":"Uncategorized","slug":"uncategorized"},{"id":2,"name":"Pokloni","slug":"pokloni"}]},
          {"id":12,"name":"Bez šifre","type":"simple","sku":"","regular_price":"","price":"","stock_quantity":null,"categories":[{"id":15,"name":"Uncategorized","slug":"uncategorized"}]},
          {"id":50,"name":"Majica basic","type":"variable","sku":"MAJ-BASIC","regular_price":"","price":"1990","stock_quantity":null,"categories":[{"id":3,"name":"Odeća","slug":"odeca"}]},
          {"id":60,"name":"Paket","type":"grouped","sku":"PAKET","regular_price":"","price":"","stock_quantity":null,"categories":[]}
        ]
        """;

    private const string Variations = """
        [
          {"id":51,"sku":"MAJ-M-SI","regular_price":"1990","price":"1990","stock_quantity":5,"attributes":[{"name":"Veličina","option":"M"},{"name":"Boja","option":"Siva"}]},
          {"id":52,"sku":"MAJ-M-BE","regular_price":"1990","price":"1990","stock_quantity":0,"attributes":[{"name":"Veličina","option":"M"},{"name":"Boja","option":"Bela"}]}
        ]
        """;

    [GeneratedRegex(@"/products/(\d+)/variations\?")]
    private static partial Regex VariationsUrl();

    /// <summary>Answers product and variation lists by URL; only page 1 has products.</summary>
    private void ShopHas(string products, string variations = Variations) =>
        factory.Shop.RespondWith(request =>
        {
            var url = request.RequestUri!.ToString();
            var firstPage = url.EndsWith("&page=1", StringComparison.Ordinal);
            if (VariationsUrl().IsMatch(url)) return FakeShopHandler.Json(firstPage ? variations : "[]");
            return FakeShopHandler.Json(firstPage ? products : "[]");
        });

    private async Task<(HttpClient Client, string ChannelId)> ConnectedShopAsync()
    {
        var client = await factory.CreateSignedInClientAsync();
        factory.Shop.RespondWith(_ => FakeShopHandler.Json("[]"));
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce",
            new { baseUrl = "zrno.rs", consumerKey = "ck_1234567890abcdef", consumerSecret = "cs_1234567890abcdef" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var channel = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (client, channel.GetProperty("id").GetString()!);
    }

    private static async Task CreateItemAsync(HttpClient client, string sku, string name = "Etiopija iz Zaliha")
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name, sku, unit = "kom", minStock = 10 });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<JsonElement> PreviewAsync(HttpClient client, string channelId)
    {
        var response = await client.GetAsync($"/api/channels/{channelId}/products/preview");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> ImportAsync(HttpClient client, string channelId, params string[] create) =>
        client.PostAsJsonAsync($"/api/channels/{channelId}/products/import", new { createExternalIds = create });

    private static async Task<JsonElement> FindItemAsync(HttpClient client, string sku)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/items?search={sku}");
        return page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("sku").GetString() == sku);
    }

    [Fact]
    public async Task Preview_ShopProducts_SortsThemIntoLinkCreateAndSkip()
    {
        // Arrange
        var (client, channelId) = await ConnectedShopAsync();
        await CreateItemAsync(client, "KF-ETI-250");
        ShopHas(Products);

        // Act
        var preview = await PreviewAsync(client, channelId);

        // Assert
        preview.GetProperty("linkedCount").GetInt32().ShouldBe(0);

        var link = preview.GetProperty("toLink").EnumerateArray().ShouldHaveSingleItem();
        link.GetProperty("externalId").GetString().ShouldBe("10");
        link.GetProperty("itemName").GetString().ShouldBe("Etiopija iz Zaliha");

        var create = preview.GetProperty("toCreate").EnumerateArray().ToList();
        create.Select(p => p.GetProperty("externalId").GetString()).ShouldBe(["11", "51", "52"]);
        create[0].GetProperty("name").GetString().ShouldBe("Proizvod & poklon");
        create[0].GetProperty("price").GetDecimal().ShouldBe(500.50m);
        create[0].GetProperty("stock").GetDecimal().ShouldBe(3);
        create[0].GetProperty("category").GetString().ShouldBe("Pokloni");
        create[1].GetProperty("name").GetString().ShouldBe("Majica basic – M, Siva");
        create[1].GetProperty("groupName").GetString().ShouldBe("Majica basic");
        create[1].GetProperty("category").GetString().ShouldBe("Odeća");
        create[2].GetProperty("category").GetString().ShouldBe("Odeća");

        var skipped = preview.GetProperty("skipped").EnumerateArray().ShouldHaveSingleItem();
        skipped.GetProperty("externalId").GetString().ShouldBe("12");
        skipped.GetProperty("error").GetProperty("code").GetString().ShouldBe("channel.product_no_sku");

        // Nothing is saved by a preview.
        (await client.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Import_ChosenProducts_CreatesItemsWithShopStockAndLinksExisting()
    {
        // Arrange
        var (client, channelId) = await ConnectedShopAsync();
        await CreateItemAsync(client, "KF-ETI-250");
        ShopHas(Products);

        // Act: create 11 and 51, but not 52
        var response = await ImportAsync(client, channelId, "11", "51");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("linkedCount").GetInt32().ShouldBe(1);
        result.GetProperty("createdCount").GetInt32().ShouldBe(2);
        result.GetProperty("skippedCount").GetInt32().ShouldBe(1);

        var gift = await FindItemAsync(client, "WOO-ONLY-1");
        gift.GetProperty("name").GetString().ShouldBe("Proizvod & poklon");
        gift.GetProperty("stock").GetDecimal().ShouldBe(3);
        gift.GetProperty("salePrice").GetDecimal().ShouldBe(500.50m);
        gift.GetProperty("unit").GetString().ShouldBe("kom");
        gift.GetProperty("minStock").GetDecimal().ShouldBe(0);

        var shirt = await FindItemAsync(client, "MAJ-M-SI");
        shirt.GetProperty("groupName").GetString().ShouldBe("Majica basic");
        shirt.GetProperty("stock").GetDecimal().ShouldBe(5);

        // The opening stock is a correction from WooCommerce, like the CSV import's.
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/items/{gift.GetProperty("id").GetString()}/history?filter=stock");
        var movement = history.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("movement");
        movement.GetProperty("type").GetString().ShouldBe("adjustment");
        movement.GetProperty("source").GetString().ShouldBe("wooCommerce");
        movement.GetProperty("quantity").GetDecimal().ShouldBe(3);
        movement.GetProperty("note").GetString().ShouldBe("Početno stanje");

        // The existing item is linked, but keeps its own name and stock.
        var existing = await FindItemAsync(client, "KF-ETI-250");
        existing.GetProperty("name").GetString().ShouldBe("Etiopija iz Zaliha");
        existing.GetProperty("stock").GetDecimal().ShouldBe(0);

        (await client.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("totalCount").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Preview_AfterImport_ShowsLinkedAndOnlyWhatIsLeft()
    {
        // Arrange
        var (client, channelId) = await ConnectedShopAsync();
        await CreateItemAsync(client, "KF-ETI-250");
        ShopHas(Products);
        (await ImportAsync(client, channelId, "11", "51")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var preview = await PreviewAsync(client, channelId);

        // Assert
        preview.GetProperty("linkedCount").GetInt32().ShouldBe(3);
        preview.GetProperty("toLink").GetArrayLength().ShouldBe(0);
        preview.GetProperty("toCreate").EnumerateArray().ShouldHaveSingleItem().GetProperty("externalId").GetString().ShouldBe("52");
    }

    [Fact]
    public async Task Import_Twice_DoesNotCreateOrLinkAgain()
    {
        // Arrange
        var (client, channelId) = await ConnectedShopAsync();
        ShopHas(Products);
        (await ImportAsync(client, channelId, "10", "11")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var result = await (await ImportAsync(client, channelId, "10", "11")).Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        result.GetProperty("linkedCount").GetInt32().ShouldBe(0);
        result.GetProperty("createdCount").GetInt32().ShouldBe(0);
        (await client.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("totalCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Preview_MoreThanOnePage_ReadsAllPages()
    {
        // Arrange: 100 products on page 1 (a full page), one more on page 2
        var (client, channelId) = await ConnectedShopAsync();
        var page1 = JsonSerializer.Serialize(Enumerable.Range(1, 100).Select(i =>
            new { id = i, name = $"Proizvod {i}", type = "simple", sku = $"P-{i}", regular_price = "100", stock_quantity = 1 }));
        var page2 = """[{"id":101,"name":"Proizvod 101","type":"simple","sku":"P-101","regular_price":"100","stock_quantity":1}]""";
        factory.Shop.RespondWith(request => FakeShopHandler.Json(
            request.RequestUri!.ToString().EndsWith("&page=1", StringComparison.Ordinal) ? page1
            : request.RequestUri.ToString().EndsWith("&page=2", StringComparison.Ordinal) ? page2
            : "[]"));

        // Act
        var preview = await PreviewAsync(client, channelId);

        // Assert
        preview.GetProperty("toCreate").GetArrayLength().ShouldBe(101);
        factory.Shop.Requests.Count.ShouldBe(2);
        factory.Shop.Requests[0].RequestUri!.ToString().ShouldContain("per_page=100");
    }

    [Fact]
    public async Task Preview_ShopRejectsKeys_ReturnsReason()
    {
        // Arrange
        var (client, channelId) = await ConnectedShopAsync();
        factory.Shop.RespondWith(_ => FakeShopHandler.Json("{}", HttpStatusCode.Unauthorized));

        // Act
        var response = await client.GetAsync($"/api/channels/{channelId}/products/preview");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.unauthorized");
    }

    [Fact]
    public async Task Import_ShopFails_SavesNothing()
    {
        // Arrange
        var (client, channelId) = await ConnectedShopAsync();
        factory.Shop.RespondWith(_ => throw new HttpRequestException("No such host"));

        // Act
        var response = await ImportAsync(client, channelId, "11");

        // Assert
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.unreachable");
        (await client.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Preview_ItemOfAnotherCompanyWithSameSku_IsNotMatched()
    {
        // Arrange: company B has KF-ETI-250, company A doesn't
        var other = await factory.CreateSignedInClientAsync();
        await CreateItemAsync(other, "KF-ETI-250");
        var (client, channelId) = await ConnectedShopAsync();
        ShopHas(Products);

        // Act
        var preview = await PreviewAsync(client, channelId);

        // Assert
        preview.GetProperty("toLink").GetArrayLength().ShouldBe(0);
        preview.GetProperty("toCreate").EnumerateArray().ShouldContain(p => p.GetProperty("externalId").GetString() == "10");
    }

    [Fact]
    public async Task PreviewAndImport_ShopOfAnotherCompany_ReturnNotFound()
    {
        // Arrange
        var (_, channelId) = await ConnectedShopAsync();
        var other = await factory.CreateSignedInClientAsync();
        ShopHas(Products);

        // Act
        var preview = await other.GetAsync($"/api/channels/{channelId}/products/preview");
        var import = await ImportAsync(other, channelId, "11");

        // Assert
        preview.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        import.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        factory.Shop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Import_Links_AreStoredWithTheVariationsParent()
    {
        // Arrange
        var (client, channelId) = await ConnectedShopAsync();
        ShopHas(Products);

        // Act
        (await ImportAsync(client, channelId, "51")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.Parse(channelId);
        var mapping = await db.ItemChannelMappings.IgnoreQueryFilters().SingleAsync(m => m.ChannelId == id);
        mapping.ExternalId.ShouldBe("51");
        mapping.ParentExternalId.ShouldBe("50");
    }
}
