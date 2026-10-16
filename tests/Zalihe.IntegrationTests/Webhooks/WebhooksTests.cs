using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Application.Common;
using Zalihe.Domain.Channels;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Webhooks;

/// <summary>
/// Webhooks end to end: the shop's HTTP request, the saved event, and the Hangfire job that applies
/// it to stock in the background (a real Hangfire server runs inside the test host).
/// </summary>
[Collection(ApiCollection.Name)]
public class WebhooksTests(ZaliheApiFactory factory)
{
    /// <summary>
    /// A WooCommerce order as webhooks send it, including the customer data that must never be stored.
    /// Lines are (product_id, variation_id, quantity); line 3 is a product deleted from the shop.
    /// </summary>
    private static string OrderJson(string status, string modifiedGmt, params (int Product, int Variation, int Quantity)[] lines) =>
        JsonSerializer.Serialize(new
        {
            id = 1041,
            status,
            currency = "RSD",
            date_created_gmt = "2026-10-16T08:59:00",
            date_modified_gmt = modifiedGmt,
            total = "4490.00",
            customer_id = 12,
            customer_note = "Pozvati pre isporuke",
            customer_ip_address = "93.87.1.2",
            billing = new { first_name = "Petar", last_name = "Petrović", address_1 = "Bulevar oslobođenja 1", city = "Novi Sad", email = "petar@example.com", phone = "0641234567" },
            shipping = new { first_name = "Petar", last_name = "Petrović", address_1 = "Bulevar oslobođenja 1", city = "Novi Sad" },
            payment_method_title = "Pouzećem",
            line_items = lines.Select((l, i) => new { id = 100 + i, name = "Proizvod", product_id = l.Product, variation_id = l.Variation, quantity = l.Quantity, sku = "X" }),
        });

    private async Task<string> SecretAsync(Guid channelId)
    {
        using var scope = factory.Services.CreateScope();
        var channel = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SalesChannels
            .IgnoreQueryFilters().SingleAsync(c => c.Id == channelId);
        return scope.ServiceProvider.GetRequiredService<ICredentialProtector>().Unprotect(channel.EncryptedWebhookSecret);
    }

    private static string Sign(string secret, string body) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

    /// <summary>Posts as the shop does: no cookie, the topic and signature in headers.</summary>
    private async Task<HttpResponseMessage> DeliverAsync(Guid channelId, string body, string? signature, string topic = "order.updated",
        string contentType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/webhooks/woo/{channelId}")
        {
            Content = new StringContent(body, Encoding.UTF8, MediaTypeHeaderValue.Parse(contentType)),
        };
        request.Headers.Add("X-WC-Webhook-Topic", topic);
        if (signature is not null) request.Headers.Add("X-WC-Webhook-Signature", signature);
        return await factory.CreateClient().SendAsync(request);
    }

    private async Task<HttpResponseMessage> DeliverSignedAsync(Guid channelId, string body, string topic = "order.updated") =>
        await DeliverAsync(channelId, body, Sign(await SecretAsync(channelId), body), topic);

    private async Task<List<WebhookEvent>> EventsAsync(Guid channelId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().WebhookEvents
            .IgnoreQueryFilters().Where(e => e.ChannelId == channelId).OrderBy(e => e.ReceivedAt).ToListAsync();
    }

    /// <summary>Waits until the background jobs have processed <paramref name="count"/> events of the shop.</summary>
    private async Task<List<WebhookEvent>> ProcessedEventsAsync(Guid channelId, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var events = await EventsAsync(channelId);
            if (events.Count(e => e.IsProcessed) >= count) return events;
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Only {events.Count(e => e.IsProcessed)} of {count} webhook events were processed.");
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task Receive_SignedOrder_LowersStockInTheBackground()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        var body = OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 2), (50, 51, 1), (0, 0, 1));

        // Act
        var response = await DeliverSignedAsync(channelId, body, "order.created");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var webhookEvent = (await ProcessedEventsAsync(channelId, 1)).ShouldHaveSingleItem();
        webhookEvent.Topic.ShouldBe("order.created");
        webhookEvent.Error.ShouldBeNull();
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(22);
        (await LinkedShop.StockAsync(client, "MAJ-M-SI")).ShouldBe(4);

        var sale = (await LinkedShop.OrderMovementsAsync(client, "KF-ETI-250")).ShouldHaveSingleItem();
        sale.GetProperty("externalRef").GetString().ShouldBe("1041");
        sale.GetProperty("occurredAt").GetDateTimeOffset().ShouldBe(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Receive_SignedOrder_StoresNoCustomerData()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);

        // Act
        await DeliverSignedAsync(channelId, OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 2)));

        // Assert
        var payload = (await EventsAsync(channelId)).ShouldHaveSingleItem().Payload;
        foreach (var personal in new[] { "Petar", "Petrović", "Bulevar", "petar@example.com", "0641234567", "93.87.1.2", "Pozvati", "Pouzećem" })
        {
            payload.ShouldNotContain(personal);
        }

        payload.ShouldContain("1041");
    }

    [Fact]
    public async Task Receive_CancelledLater_ReturnsStock()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        await DeliverSignedAsync(channelId, OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 2), (50, 51, 1)), "order.created");
        await ProcessedEventsAsync(channelId, 1);

        // Act
        await DeliverSignedAsync(channelId, OrderJson("cancelled", "2026-10-16T09:30:00", (10, 0, 2), (50, 51, 1)));

        // Assert
        await ProcessedEventsAsync(channelId, 2);
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(24);
        (await LinkedShop.StockAsync(client, "MAJ-M-SI")).ShouldBe(5);
    }

    [Fact]
    public async Task Receive_SameDeliveryTwice_TakesStockOnce()
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);
        var body = OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 2));

        // Act: the shop retries a delivery it thinks failed
        await DeliverSignedAsync(channelId, body);
        await DeliverSignedAsync(channelId, body);

        // Assert
        await ProcessedEventsAsync(channelId, 2);
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(22);
        (await LinkedShop.OrderMovementsAsync(client, "KF-ETI-250")).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("bm90IHRoZSByaWdodCBzaWduYXR1cmU=")]
    [InlineData("not base64 at all")]
    [InlineData(null)]
    public async Task Receive_WrongOrMissingSignature_Returns401AndStoresNothing(string? signature)
    {
        // Arrange
        var (client, channelId) = await LinkedShop.CreateAsync(factory);

        // Act
        var response = await DeliverAsync(channelId, OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 2)), signature);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EventsAsync(channelId)).ShouldBeEmpty();
        (await LinkedShop.StockAsync(client, "KF-ETI-250")).ShouldBe(24);
    }

    [Fact]
    public async Task Receive_BodyChangedAfterSigning_Returns401()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);
        var signature = Sign(await SecretAsync(channelId), OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 2)));

        // Act: someone raised the quantity on the way
        var response = await DeliverAsync(channelId, OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 20)), signature);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Receive_SignedWithAnotherShopsSecret_Returns401()
    {
        // Arrange: company B signs with its own secret and sends to company A's shop
        var (clientA, channelA) = await LinkedShop.CreateAsync(factory);
        var (_, channelB) = await LinkedShop.CreateAsync(factory);
        var body = OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 2));

        // Act
        var response = await DeliverAsync(channelA, body, Sign(await SecretAsync(channelB), body));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EventsAsync(channelA)).ShouldBeEmpty();
        (await LinkedShop.StockAsync(clientA, "KF-ETI-250")).ShouldBe(24);
    }

    [Fact]
    public async Task Receive_Ping_Returns200WithoutLookingAtTheShop()
    {
        // Act: WooCommerce pings before the channel is even saved
        var response = await DeliverAsync(Guid.NewGuid(), "webhook_id=17", signature: null,
            contentType: "application/x-www-form-urlencoded");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Receive_UnknownShop_Returns404()
    {
        // Act
        var response = await DeliverAsync(Guid.NewGuid(), OrderJson("processing", "2026-10-16T09:00:00", (10, 0, 2)), "c2lnbmF0dXJl");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("product.updated")]
    [InlineData("")]
    public async Task Receive_NotAnOrderTopic_Returns200AndStoresNothing(string topic)
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);

        // Act
        var response = await DeliverSignedAsync(channelId, """{"id":10,"name":"Kafa"}""", topic);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await EventsAsync(channelId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Receive_OrderMissingFields_Returns200AndStoresNothing()
    {
        // Arrange
        var (_, channelId) = await LinkedShop.CreateAsync(factory);

        // Act
        var response = await DeliverSignedAsync(channelId, """{"id":1041,"status":"processing"}""");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await EventsAsync(channelId)).ShouldBeEmpty();
    }
}
