using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Items;

[Collection(ApiCollection.Name)]
public class ItemUpdateTests(ZaliheApiFactory factory)
{
    private static object Item(string sku = "KF-ETI-250", string name = "Kafa Etiopija 250 g", decimal minStock = 10) =>
        new { name, sku, unit = "kom", category = "Kafa · zrno", minStock };

    private static async Task<string> CreateAsync(HttpClient client, object item)
    {
        var response = await client.PostAsJsonAsync("/api/items", item);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<string[]> ListSkusAsync(HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/items{query}")).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("sku").GetString()!)
            .ToArray();

    [Fact]
    public async Task Update_ValidData_ChangesItem()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var id = await CreateAsync(client, Item());

        // Act
        var response = await client.PutAsJsonAsync($"/api/items/{id}",
            new { name = "Kafa Etiopija 1 kg", sku = "KF-ETI-1000", unit = "kg", minStock = 2.5m, salePrice = 3200 });
        var stored = await client.GetFromJsonAsync<JsonElement>($"/api/items/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        stored.GetProperty("name").GetString().ShouldBe("Kafa Etiopija 1 kg");
        stored.GetProperty("sku").GetString().ShouldBe("KF-ETI-1000");
        stored.GetProperty("unit").GetString().ShouldBe("kg");
        stored.GetProperty("minStock").GetDecimal().ShouldBe(2.5m);
        stored.GetProperty("salePrice").GetDecimal().ShouldBe(3200);
        stored.GetProperty("category").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Update_KeepsOwnSku_Succeeds()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var id = await CreateAsync(client, Item());

        // Act
        var response = await client.PutAsJsonAsync($"/api/items/{id}", Item(name: "Novi naziv"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_SkuOfAnotherItem_ReturnsSkuDuplicate()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        await CreateAsync(client, Item("A-1", "Prvi"));
        var id = await CreateAsync(client, Item("B-1", "Drugi"));

        // Act
        var response = await client.PutAsJsonAsync($"/api/items/{id}", Item("A-1", "Drugi"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe("item.sku_duplicate");
    }

    [Fact]
    public async Task Update_ItemOfOtherCompany_Returns404AndLeavesItUnchanged()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        var id = await CreateAsync(companyA, Item());

        // Act
        var response = await companyB.PutAsJsonAsync($"/api/items/{id}", Item(name: "Preuzeto"));
        var stored = await companyA.GetFromJsonAsync<JsonElement>($"/api/items/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        stored.GetProperty("name").GetString().ShouldBe("Kafa Etiopija 250 g");
    }

    [Fact]
    public async Task Update_UnknownItem_Returns404()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PutAsJsonAsync($"/api/items/{Guid.NewGuid()}", Item());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_Item_HiddenFromListUnlessInactiveIncluded()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var id = await CreateAsync(client, Item("A-1"));
        await CreateAsync(client, Item("B-1"));

        // Act
        var response = await client.PostAsync($"/api/items/{id}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListSkusAsync(client)).ShouldBe(["B-1"]);
        (await ListSkusAsync(client, "?includeInactive=true")).ShouldBe(["A-1", "B-1"], ignoreOrder: true);
        var stored = await client.GetFromJsonAsync<JsonElement>($"/api/items/{id}");
        stored.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Activate_DeactivatedItem_ShowsInListAgain()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var id = await CreateAsync(client, Item());
        await client.PostAsync($"/api/items/{id}/deactivate", null);

        // Act
        var response = await client.PostAsync($"/api/items/{id}/activate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListSkusAsync(client)).ShouldBe(["KF-ETI-250"]);
    }

    [Fact]
    public async Task Deactivate_ItemOfOtherCompany_Returns404AndItStaysActive()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        var id = await CreateAsync(companyA, Item());

        // Act
        var response = await companyB.PostAsync($"/api/items/{id}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListSkusAsync(companyA)).ShouldBe(["KF-ETI-250"]);
    }

    [Fact]
    public async Task Create_SkuOfDeactivatedItem_ReturnsSkuDuplicate()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var id = await CreateAsync(client, Item());
        await client.PostAsync($"/api/items/{id}/deactivate", null);

        // Act
        var response = await client.PostAsJsonAsync("/api/items", Item(name: "Novi artikal"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString()
            .ShouldBe("item.sku_duplicate");
    }
}
