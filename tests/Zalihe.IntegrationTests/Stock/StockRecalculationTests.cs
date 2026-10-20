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
public class StockRecalculationTests(ZaliheApiFactory factory)
{
    private static async Task<Guid> ItemAsync(HttpClient client, string sku, decimal receipt)
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name = $"Artikal {sku}", sku, unit = "kom", minStock = 0 });
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "receipt", quantity = receipt })).EnsureSuccessStatusCode();
        return id;
    }

    /// <summary>Breaks the rule on purpose: the cached stock no longer equals the movements.</summary>
    private async Task CorruptStockAsync(Guid itemId, decimal quantity)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"""UPDATE "StockLevels" SET "Quantity" = {quantity} WHERE "ItemId" = {itemId}""");
    }

    private static async Task<decimal> StockAsync(HttpClient client, Guid itemId) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}")).GetProperty("stock").GetDecimal();

    private static async Task<JsonElement> RecalculateAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/stock/recalculate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Recalculate_StockThatDiffersFromMovements_IsSetBackToTheirSum()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var broken = await ItemAsync(client, "POKVARENO", receipt: 10);
        (await client.PostAsJsonAsync($"/api/items/{broken}/movements", new { kind = "sale", quantity = 2.5 })).EnsureSuccessStatusCode();
        var fine = await ItemAsync(client, "ISPRAVNO", receipt: 4);
        await CorruptStockAsync(broken, 99);

        // Act
        var result = await RecalculateAsync(client);

        // Assert
        result.GetProperty("checkedCount").GetInt32().ShouldBe(2);
        var corrected = result.GetProperty("corrected").EnumerateArray().ShouldHaveSingleItem();
        corrected.GetProperty("sku").GetString().ShouldBe("POKVARENO");
        corrected.GetProperty("before").GetDecimal().ShouldBe(99);
        corrected.GetProperty("after").GetDecimal().ShouldBe(7.5m);
        (await StockAsync(client, broken)).ShouldBe(7.5m);
        (await StockAsync(client, fine)).ShouldBe(4);
    }

    [Fact]
    public async Task Recalculate_EverythingMatches_ChangesNothingAndAddsNoMovements()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var item = await ItemAsync(client, "KF-1", receipt: 10);

        // Act
        var result = await RecalculateAsync(client);

        // Assert
        result.GetProperty("corrected").GetArrayLength().ShouldBe(0);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/items/{item}/history?filter=stock");
        history.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Recalculate_LinkedProduct_MarksItsStockForTheShop()
    {
        // Arrange
        var (client, _) = await LinkedShop.CreateAsync(factory);
        var coffee = (await LinkedShop.ItemAsync(client, "KF-ETI-250")).GetProperty("id").GetGuid();
        await CorruptStockAsync(coffee, 3);

        // Act
        await RecalculateAsync(client);

        // Assert
        using var scope = factory.Services.CreateScope();
        var mapping = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ItemChannelMappings
            .IgnoreQueryFilters().SingleAsync(m => m.ItemId == coffee);
        mapping.StockChangedAt.ShouldNotBeNull();
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(24);
    }

    [Fact]
    public async Task Recalculate_AnotherCompanysBrokenStock_IsLeftForThatCompany()
    {
        // Arrange
        var other = await factory.CreateSignedInClientAsync();
        var theirs = await ItemAsync(other, "TUDJE", receipt: 5);
        await CorruptStockAsync(theirs, 50);
        var client = await factory.CreateSignedInClientAsync();
        await ItemAsync(client, "MOJE", receipt: 1);

        // Act
        var result = await RecalculateAsync(client);

        // Assert
        result.GetProperty("checkedCount").GetInt32().ShouldBe(1);
        result.GetProperty("corrected").GetArrayLength().ShouldBe(0);
        (await StockAsync(other, theirs)).ShouldBe(50);
    }

    [Fact]
    public async Task Recalculate_Anonymous_Returns401()
    {
        (await factory.CreateClient().PostAsync("/api/stock/recalculate", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
