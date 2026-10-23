using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Application.Channels;
using Zalihe.Application.Orders;
using Zalihe.Infrastructure.Persistence;
using Zalihe.Infrastructure.Tenancy;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Channels;

/// <summary>
/// The shop card's figures. Products of <see cref="LinkedShop"/>: coffee 10 (KF-ETI-250) and
/// T-shirt variation 51 of 50 (MAJ-M-SI), both linked.
/// </summary>
[Collection(ApiCollection.Name)]
public class ChannelSummaryTests(ZaliheApiFactory factory)
{
    private async Task ProcessAsync(Guid channelId, ShopOrder order)
    {
        using var scope = factory.Services.CreateScope();
        var tenantId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().FindChannelTenantAsync(channelId, CancellationToken.None);
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId!.Value);
        await scope.ServiceProvider.GetRequiredService<OrderService>().ProcessAsync(channelId, order, CancellationToken.None);
    }

    private static ShopOrder Order(string number, params ShopOrderLine[] lines) =>
        new(number, "processing", DateTimeOffset.UtcNow.AddMinutes(1), DateTimeOffset.UtcNow, lines);

    [Fact]
    public async Task GetSummary_OrdersAndAStockChange_CountsWhatTheCardShows()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await ProcessAsync(channelId, Order("1041", new ShopOrderLine("51", 1)));
        await ProcessAsync(channelId, Order("1042", new ShopOrderLine("99", 2)));   // product 99 is not linked

        // Act
        var summary = await client.GetFromJsonAsync<JsonElement>($"/api/channels/{channelId}/summary");

        // Assert
        summary.GetProperty("linkedProducts").GetInt32().ShouldBe(2);
        summary.GetProperty("ordersWithUnlinked").GetInt32().ShouldBe(1);
        summary.GetProperty("lastOrderNumber").GetString().ShouldBe("1042");
        summary.GetProperty("lastOrderAt").ValueKind.ShouldBe(JsonValueKind.String);
        summary.GetProperty("pendingStock").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task GetSummary_NoOrdersYet_HasNoLastOrder()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);

        // Act
        var summary = await client.GetFromJsonAsync<JsonElement>($"/api/channels/{channelId}/summary");

        // Assert
        summary.GetProperty("lastOrderAt").ValueKind.ShouldBe(JsonValueKind.Null);
        summary.GetProperty("ordersWithUnlinked").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetSummary_OtherCompanysShop_ReturnsNotFound()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);
        var other = await factory.CreateSignedInClientAsync();

        // Act
        var response = await other.GetAsync($"/api/channels/{channelId}/summary");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDashboard_OrderWithUnlinkedProduct_CountsIt()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await ProcessAsync(channelId, Order("1041", new ShopOrderLine("51", 1)));
        await ProcessAsync(channelId, Order("1042", new ShopOrderLine("99", 2)));
        await ProcessAsync(channelId, Order("1043", new ShopOrderLine("98", 1), new ShopOrderLine("10", 1)));

        // Act
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");

        // Assert
        dashboard.GetProperty("ordersWithUnlinked").GetInt32().ShouldBe(2);
    }
}
