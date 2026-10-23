using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Application.Channels;
using Zalihe.Application.Orders;
using Zalihe.Domain.Channels;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Infrastructure.Tenancy;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Channels;

/// <summary>
/// Stock sent from Zalihe to the shop (SPEC 7.5). Stock changes mark the linked products; the push is
/// run the way its job runs it (own scope, company set), since scheduled pushes are off in tests.
/// Products of <see cref="LinkedShop"/>: coffee 10 (KF-ETI-250, 24) and T-shirt variation 51 of 50 (MAJ-M-SI, 5).
/// </summary>
[Collection(ApiCollection.Name)]
public class StockPushTests(ZaliheApiFactory factory)
{
    /// <summary>The shop accepts every product in a batch, as WooCommerce answers it.</summary>
    private void ShopAcceptsAll() => factory.Shop.RespondWith(request =>
    {
        var body = request.Content!.ReadAsStringAsync().Result;
        var ids = JsonDocument.Parse(body).RootElement.GetProperty("update").EnumerateArray().Select(u => u.GetProperty("id").GetInt64());
        return FakeShopHandler.Json(JsonSerializer.Serialize(new { update = ids.Select(id => new { id }) }));
    });

    private static async Task MoveAsync(HttpClient client, string sku, string kind, decimal quantity)
    {
        var id = (await LinkedShop.ItemAsync(client, sku)).GetProperty("id").GetString();
        var response = await client.PostAsJsonAsync($"/api/items/{id}/movements", new { kind, quantity });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<Dictionary<string, ItemChannelMapping>> MappingsAsync(Guid channelId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ItemChannelMappings
            .IgnoreQueryFilters().AsNoTracking().Where(m => m.ChannelId == channelId)
            .ToDictionaryAsync(m => m.ExternalId);
    }

    private async Task<SalesChannel> ChannelAsync(Guid channelId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SalesChannels
            .IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == channelId);
    }

    private async Task<Guid> TenantOfAsync(Guid channelId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().FindChannelTenantAsync(channelId, CancellationToken.None)
            ?? throw new InvalidOperationException("No such shop.");
    }

    private async Task<StockPushResult> PushAsync(Guid channelId, Guid? asTenant = null)
    {
        var tenantId = asTenant ?? await TenantOfAsync(channelId);
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<StockPushService>().PushAsync(channelId, CancellationToken.None);
    }

    [Fact]
    public async Task RecordMovement_LinkedItem_MarksOnlyItsShopProduct()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);

        // Act
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);

        // Assert
        var mappings = await MappingsAsync(channelId);
        mappings["10"].StockChangedAt.ShouldNotBeNull();
        mappings["51"].StockChangedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessOrder_LinkedItems_MarksTheirShopProducts()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(await TenantOfAsync(channelId));
        var order = new ShopOrder("1041", "processing", DateTimeOffset.UtcNow.AddMinutes(1), DateTimeOffset.UtcNow, [new ShopOrderLine("51", 1)]);

        // Act
        await scope.ServiceProvider.GetRequiredService<OrderService>().ProcessAsync(channelId, order, CancellationToken.None);

        // Assert
        var mappings = await MappingsAsync(channelId);
        mappings["51"].StockChangedAt.ShouldNotBeNull();
        mappings["10"].StockChangedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Push_ChangedProducts_SendsWholeStockPerProductTypeAndClearsMarks()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);
        await MoveAsync(client, "MAJ-M-SI", "sale", 1);
        ShopAcceptsAll();

        // Act
        var result = await PushAsync(channelId);

        // Assert
        result.ShouldBe(new StockPushResult(Sent: 2, Unchanged: 0, Rejected: 0));
        var sent = factory.Shop.Requests.Zip(factory.Shop.Bodies)
            .ToDictionary(r => r.First.RequestUri!.AbsolutePath, r => JsonDocument.Parse(r.Second!).RootElement.GetProperty("update")[0]);
        factory.Shop.Requests.ShouldAllBe(r => r.Method == HttpMethod.Post);

        var coffee = sent["/wp-json/wc/v3/products/batch"];
        coffee.GetProperty("id").GetInt64().ShouldBe(10);
        coffee.GetProperty("manage_stock").GetBoolean().ShouldBeTrue();
        coffee.GetProperty("stock_quantity").GetInt32().ShouldBe(27);

        var shirt = sent["/wp-json/wc/v3/products/50/variations/batch"];
        shirt.GetProperty("id").GetInt64().ShouldBe(51);
        shirt.GetProperty("stock_quantity").GetInt32().ShouldBe(4);

        var mappings = await MappingsAsync(channelId);
        mappings["10"].StockChangedAt.ShouldBeNull();
        mappings["10"].PushedStock.ShouldBe(27);
        mappings["51"].PushedStock.ShouldBe(4);
    }

    [Fact]
    public async Task Push_StockBackToWhatShopHas_SendsNothingAndClearsMark()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);
        ShopAcceptsAll();
        await PushAsync(channelId);
        await MoveAsync(client, "KF-ETI-250", "sale", 1);
        await MoveAsync(client, "KF-ETI-250", "return", 1);
        ShopAcceptsAll();

        // Act
        var result = await PushAsync(channelId);

        // Assert
        result.ShouldBe(new StockPushResult(Sent: 0, Unchanged: 1, Rejected: 0));
        factory.Shop.Requests.ShouldBeEmpty();
        (await MappingsAsync(channelId))["10"].StockChangedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Push_ShopRejectsProduct_KeepsItMarkedForNextPush()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);
        factory.Shop.RespondWith(_ => FakeShopHandler.Json(
            """{"update":[{"id":0,"error":{"code":"woocommerce_rest_product_invalid_id","message":"Invalid ID.","data":{"status":400}}}]}"""));

        // Act
        var result = await PushAsync(channelId);

        // Assert
        result.ShouldBe(new StockPushResult(Sent: 0, Unchanged: 0, Rejected: 1));
        var mapping = (await MappingsAsync(channelId))["10"];
        mapping.StockChangedAt.ShouldNotBeNull();
        mapping.PushedStock.ShouldBeNull();
    }

    [Fact]
    public async Task Push_ShopUnreachable_ThrowsAndKeepsMarks()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);
        factory.Shop.RespondWith(_ => throw new HttpRequestException("Connection refused"));

        // Act
        var error = await Should.ThrowAsync<SalesChannelException>(() => PushAsync(channelId));

        // Assert
        error.Code.ShouldBe(SalesChannelException.Unreachable);
        (await MappingsAsync(channelId))["10"].StockChangedAt.ShouldNotBeNull();
        var channel = await ChannelAsync(channelId);
        channel.LastErrorCode.ShouldBe(SalesChannelException.Unreachable);
        channel.FailingSince.ShouldNotBeNull();
    }

    [Fact]
    public async Task Push_ShopAnswersAfterFailure_ClearsFailure()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);
        factory.Shop.RespondWith(_ => throw new HttpRequestException("Connection refused"));
        await Should.ThrowAsync<SalesChannelException>(() => PushAsync(channelId));
        ShopAcceptsAll();

        // Act
        await PushAsync(channelId);

        // Assert
        var channel = await ChannelAsync(channelId);
        channel.LastErrorCode.ShouldBeNull();
        channel.FailingSince.ShouldBeNull();
    }

    [Fact]
    public async Task Push_StockChangesWhileSending_NewChangeStaysMarked()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);
        factory.Shop.RespondWith(_ =>
        {
            // A sale is recorded while the shop is answering.
            MoveAsync(client, "KF-ETI-250", "sale", 1).GetAwaiter().GetResult();
            return FakeShopHandler.Json("""{"update":[{"id":10}]}""");
        });

        // Act
        await PushAsync(channelId);

        // Assert: 27 reached the shop, and the sale (26) is still to be sent.
        var mapping = (await MappingsAsync(channelId))["10"];
        mapping.StockChangedAt.ShouldNotBeNull();
        mapping.PushedStock.ShouldBeNull();
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(26);
    }

    [Fact]
    public async Task Push_OtherCompanysShop_SendsNothing()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);
        var (_, otherChannelId) = await LinkedShop.CreateAsync(factory);
        ShopAcceptsAll();

        // Act
        var result = await PushAsync(channelId, asTenant: await TenantOfAsync(otherChannelId));

        // Assert
        result.ShouldBe(new StockPushResult(0, 0, 0));
        factory.Shop.Requests.ShouldBeEmpty();
        (await MappingsAsync(channelId))["10"].StockChangedAt.ShouldNotBeNull();
    }

    private static async Task<JsonElement> ShopStockAsync(HttpClient client, string sku)
    {
        var id = (await LinkedShop.ItemAsync(client, sku)).GetProperty("id").GetString();
        return await client.GetFromJsonAsync<JsonElement>($"/api/items/{id}/shop-stock");
    }

    [Fact]
    public async Task GetShopStock_ChangeNotSentYet_IsPending()
    {
        // Arrange
        var (client, _) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);

        // Act
        var status = await ShopStockAsync(client, "KF-ETI-250");

        // Assert
        status.GetProperty("shops").GetInt32().ShouldBe(1);
        status.GetProperty("pending").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GetShopStock_ChangeSent_IsNotPending()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await MoveAsync(client, "KF-ETI-250", "receipt", 3);
        ShopAcceptsAll();
        await PushAsync(channelId);

        // Act
        var status = await ShopStockAsync(client, "KF-ETI-250");

        // Assert
        status.GetProperty("shops").GetInt32().ShouldBe(1);
        status.GetProperty("pending").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task GetShopStock_OtherCompanysItem_ReturnsNotFound()
    {
        // Arrange
        var (client, _) = await LinkedShop.CreateAsync(factory);
        var id = (await LinkedShop.ItemAsync(client, "KF-ETI-250")).GetProperty("id").GetString();
        var other = await factory.CreateSignedInClientAsync();

        // Act
        var response = await other.GetAsync($"/api/items/{id}/shop-stock");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
