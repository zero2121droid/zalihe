using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Items;

[Collection(ApiCollection.Name)]
public class ItemsTests(ZaliheApiFactory factory)
{
    private static object NewItem(string sku = "KF-ETI-250", string name = "Kafa Etiopija 250 g") => new
    {
        name,
        sku,
        unit = "kom",
        category = "Kafa · zrno",
        purchasePrice = 900.5m,
        minStock = 10,
    };

    private static async Task<JsonElement> CreateAsync(HttpClient client, object item)
    {
        var response = await client.PostAsJsonAsync("/api/items", item);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> ListAsync(HttpClient client, string query = "") =>
        await client.GetFromJsonAsync<JsonElement>($"/api/items{query}");

    [Fact]
    public async Task Create_ValidItem_Returns201AndItemAppearsInList()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/items", NewItem());
        var list = await ListAsync(client);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("unit").GetString().ShouldBe("kom");
        created.GetProperty("purchasePrice").GetDecimal().ShouldBe(900.5m);
        created.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        list.GetProperty("totalCount").GetInt32().ShouldBe(1);
        list.GetProperty("items")[0].GetProperty("sku").GetString().ShouldBe("KF-ETI-250");
    }

    [Fact]
    public async Task Create_DuplicateSkuInSameCompany_ReturnsSkuDuplicate()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        await CreateAsync(client, NewItem());

        // Act
        var response = await client.PostAsJsonAsync("/api/items", NewItem(name: "Druga kafa"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("item.sku_duplicate");
        error.GetProperty("field").GetString().ShouldBe("sku");
    }

    [Fact]
    public async Task Create_SameSkuInDifferentCompany_Succeeds()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        await CreateAsync(companyA, NewItem());

        // Act
        var response = await companyB.PostAsJsonAsync("/api/items", NewItem());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task List_OtherCompanyHasItems_SeesOnlyOwnItems()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        await CreateAsync(companyA, NewItem("A-1", "Artikal firme A"));
        await CreateAsync(companyB, NewItem("B-1", "Artikal firme B"));

        // Act
        var listA = await ListAsync(companyA);
        var categoriesB = await companyB.GetFromJsonAsync<string[]>("/api/items/categories");
        var searchB = await ListAsync(companyB, "?search=firme");

        // Assert
        listA.GetProperty("totalCount").GetInt32().ShouldBe(1);
        listA.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Artikal firme A");
        searchB.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()).ShouldBe(["B-1"]);
        categoriesB.ShouldBe(["Kafa · zrno"]);
    }

    [Fact]
    public async Task Get_ItemOfOtherCompany_Returns404()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        var item = await CreateAsync(companyA, NewItem());
        var id = item.GetProperty("id").GetString();

        // Act
        var own = await companyA.GetAsync($"/api/items/{id}");
        var foreign = await companyB.GetAsync($"/api/items/{id}");

        // Assert
        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await foreign.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("common.not_found");
    }

    [Theory]
    [InlineData("etiop", "KF-ETI-250")]
    [InlineData("kol-250", "KF-KOL-250")]
    [InlineData("860123", "KF-KOL-250")]
    public async Task List_Search_MatchesNameSkuOrBarcodeIgnoringCase(string search, string expectedSku)
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        await CreateAsync(client, NewItem("KF-ETI-250", "Kafa Etiopija 250 g"));
        await CreateAsync(client, new { name = "Kafa Kolumbija 250 g", sku = "KF-KOL-250", unit = "kom", barcode = "8601234567890" });

        // Act
        var list = await ListAsync(client, $"?search={search}");

        // Assert
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()).ShouldBe([expectedSku]);
    }

    [Fact]
    public async Task List_Paging_ReturnsRequestedPageSortedByName()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        foreach (var letter in new[] { "C", "A", "E", "B", "D" })
        {
            await CreateAsync(client, NewItem($"SKU-{letter}", $"Artikal {letter}"));
        }

        // Act
        var page2 = await ListAsync(client, "?page=2&pageSize=2");

        // Assert
        page2.GetProperty("totalCount").GetInt32().ShouldBe(5);
        page2.GetProperty("page").GetInt32().ShouldBe(2);
        page2.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString())
            .ShouldBe(["Artikal C", "Artikal D"]);
    }

    [Fact]
    public async Task List_PageSizeOverLimit_ReturnsValidationError()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.GetAsync("/api/items?pageSize=1000");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("validation.out_of_range");
        error.GetProperty("field").GetString().ShouldBe("pageSize");
    }

    [Fact]
    public async Task Create_InvalidValues_ReturnsCodesPerField()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/items",
            new { name = "", sku = "X-1", unit = "kom", minStock = -1, salePrice = -5 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray()
            .Select(e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()));
        errors.ShouldBe(
            [("name", "validation.required"), ("minStock", "validation.not_negative"), ("salePrice", "validation.not_negative")],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Create_TooManyDecimals_ReturnsTooManyDecimalsWithMaximum()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/items",
            new { name = "Kafa", sku = "X-1", unit = "kg", minStock = 1.2345m, purchasePrice = 9.999m });

        // Assert
        var errors = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ToList();
        errors.Select(e => e.GetProperty("field").GetString()).ShouldBe(["minStock", "purchasePrice"], ignoreOrder: true);
        errors.ShouldAllBe(e => e.GetProperty("code").GetString() == "validation.too_many_decimals");
        errors.Single(e => e.GetProperty("field").GetString() == "minStock")
            .GetProperty("params").GetProperty("max").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Create_UnknownUnit_ReturnsInvalidValueForUnit()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/items", new { name = "Kafa", sku = "X-1", unit = "tona" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem.GetProperty("errors").EnumerateArray()
            .ShouldContain(e => e.GetProperty("code").GetString() == "validation.invalid");
    }

    [Fact]
    public async Task Create_WithoutAntiforgeryToken_ReturnsAntiforgeryInvalid()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.RegisterAsync(HttpClientExtensions.UniqueEmail());

        // Act
        var response = await client.PostAsJsonAsync("/api/items", NewItem());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.antiforgery_invalid");
    }

    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        // Act
        var response = await factory.CreateClient().GetAsync("/api/items");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
