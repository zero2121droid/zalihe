using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Items;

[Collection(ApiCollection.Name)]
public class ItemHistoryTests(ZaliheApiFactory factory)
{
    private static object Item(decimal minStock = 10, decimal? purchasePrice = 900) =>
        new { name = "Kafa Etiopija 250 g", sku = "KF-ETI-250", unit = "kom", category = "Kafa", minStock, purchasePrice };

    private static async Task<string> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/items", Item());
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement[]> HistoryAsync(HttpClient client, string itemId, string query = "") =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}/history{query}"))
            .GetProperty("items").EnumerateArray().ToArray();

    private static string? Kind(JsonElement entry) =>
        entry.GetProperty("change").ValueKind == JsonValueKind.Null
            ? "movement:" + entry.GetProperty("movement").GetProperty("type").GetString()
            : entry.GetProperty("change").GetProperty("kind").GetString();

    [Fact]
    public async Task Create_NewItem_LogsCreatedByCurrentUser()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var history = await HistoryAsync(client, await CreateAsync(client));

        // Assert
        var entry = history.ShouldHaveSingleItem();
        Kind(entry).ShouldBe("created");
        entry.GetProperty("userName").GetString().ShouldBe("Miljan");
        entry.GetProperty("movement").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Update_ChangedFields_LogsOldAndNewValues()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateAsync(client);

        // Act
        await client.PutAsJsonAsync($"/api/items/{itemId}", Item(minStock: 12.5m, purchasePrice: null));
        var history = await HistoryAsync(client, itemId);

        // Assert
        Kind(history[0]).ShouldBe("updated");
        history[0].GetProperty("change").GetProperty("changes").EnumerateArray()
            .Select(c => (c.GetProperty("field").GetString(), c.GetProperty("oldValue").GetString(), c.GetProperty("newValue").GetString()))
            .ShouldBe([("purchasePrice", "900", null), ("minStock", "10", "12.5")]);
    }

    [Fact]
    public async Task Update_SameData_LogsNothing()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateAsync(client);

        // Act
        await client.PutAsJsonAsync($"/api/items/{itemId}", Item());

        // Assert
        (await HistoryAsync(client, itemId)).Select(Kind).ShouldBe(["created"]);
    }

    [Fact]
    public async Task DeactivateTwiceThenActivate_LogsEachRealChangeOnce()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateAsync(client);

        // Act
        await client.PostAsync($"/api/items/{itemId}/deactivate", null);
        await client.PostAsync($"/api/items/{itemId}/deactivate", null);
        await client.PostAsync($"/api/items/{itemId}/activate", null);

        // Assert
        (await HistoryAsync(client, itemId)).Select(Kind).ShouldBe(["activated", "deactivated", "created"]);
    }

    [Fact]
    public async Task History_MovementsAndChanges_MergedNewestFirstAndFilterable()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateAsync(client);
        await client.PostAsJsonAsync($"/api/items/{itemId}/movements", new { kind = "receipt", quantity = 24 });
        await client.PutAsJsonAsync($"/api/items/{itemId}", Item(minStock: 5));
        await client.PostAsJsonAsync($"/api/items/{itemId}/movements", new { kind = "sale", quantity = 2 });

        // Act
        var all = await HistoryAsync(client, itemId);
        var stock = await HistoryAsync(client, itemId, "?filter=stock");
        var changes = await HistoryAsync(client, itemId, "?filter=changes");
        var page2 = await HistoryAsync(client, itemId, "?pageSize=3&page=2");

        // Assert
        all.Select(Kind).ShouldBe(["movement:sale", "updated", "movement:receipt", "created"]);
        stock.Select(Kind).ShouldBe(["movement:sale", "movement:receipt"]);
        changes.Select(Kind).ShouldBe(["updated", "created"]);
        page2.Select(Kind).ShouldBe(["created"]);
    }

    [Fact]
    public async Task History_ItemOfOtherCompany_Returns404()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        var itemId = await CreateAsync(companyA);

        // Act
        var response = await companyB.GetAsync($"/api/items/{itemId}/history");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
