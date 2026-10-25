using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Channels;

/// <summary>Connecting a WooCommerce shop through its approval page (wc-auth, SPEC 7.1).</summary>
[Collection(ApiCollection.Name)]
public class ShopAuthorizationTests(ZaliheApiFactory factory)
{
    private const string Key = "ck_1234567890abcdef";
    private const string Secret = "cs_1234567890abcdef";

    private record Started(Guid Id, string Token, Uri Url, Dictionary<string, string> Query);

    private async Task<Started> StartAsync(HttpClient client, string baseUrl = "zrno.rs")
    {
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce/authorizations", new { baseUrl });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = new Uri(body.GetProperty("authorizeUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(url.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
        return new Started(Guid.Parse(body.GetProperty("id").GetString()!), query["user_id"], url, query);
    }

    /// <summary>What WooCommerce posts to the callback address after the owner clicks "Approve".</summary>
    private Task<HttpResponseMessage> ShopSendsKeysAsync(string token, string key = Key, string secret = Secret) =>
        factory.CreateClient().PostAsync("/webhooks/woo/authorize", new StringContent(
            JsonSerializer.Serialize(new { key_id = 7, user_id = token, consumer_key = key, consumer_secret = secret, key_permissions = "read_write" }),
            Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, Guid id) =>
        client.PostAsync($"/api/channels/woocommerce/authorizations/{id}/complete", null);

    private void ShopAccepts() => factory.Shop.RespondWith(_ => FakeShopHandler.Json("[{\"id\":10}]"));

    private async Task ExpireAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().ShopAuthorizations.IgnoreQueryFilters()
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public async Task Start_ValidAddress_ReturnsShopApprovalPageWithCallbackAndReturnAddress()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAccepts();

        // Act
        var started = await StartAsync(client, "zrno.rs/");

        // Assert
        started.Url.GetLeftPart(UriPartial.Path).ShouldBe("https://zrno.rs/wc-auth/v1/authorize");
        started.Query["app_name"].ShouldBe("Lagerko");
        started.Query["scope"].ShouldBe("read_write");
        started.Query["callback_url"].ShouldBe($"{ZaliheApiFactory.WebhookBaseUrl}/webhooks/woo/authorize");
        started.Query["return_url"].ShouldBe($"https://app.zalihe.test/channels?authorization={started.Id}");
        started.Token.Length.ShouldBeGreaterThanOrEqualTo(43);
        factory.Shop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Start_InvalidAddress_ReturnsFieldError()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce/authorizations", new { baseUrl = "http://zrno.rs" });

        // Assert
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("channel.url_invalid");
        error.GetProperty("field").GetString().ShouldBe("baseUrl");
    }

    [Fact]
    public async Task Start_ShopAlreadyConnected_ReturnsAlreadyConnected()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAccepts();
        (await client.PostAsJsonAsync("/api/channels/woocommerce", new { baseUrl = "zrno.rs", consumerKey = Key, consumerSecret = Secret }))
            .EnsureSuccessStatusCode();

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce/authorizations", new { baseUrl = "zrno.rs" });

        // Assert
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.already_connected");
    }

    [Fact]
    public async Task Complete_ShopSentKeys_SavesChannelWithTheShopsKeys()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAccepts();
        var started = await StartAsync(client);
        (await ShopSendsKeysAsync(started.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var response = await CompleteAsync(client, started.Id);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("baseUrl").GetString().ShouldBe("https://zrno.rs");
        factory.Shop.Requests.ShouldNotBeEmpty();
        factory.Shop.Requests.Select(r => Encoding.UTF8.GetString(Convert.FromBase64String(r.Headers.Authorization!.Parameter!)))
            .ShouldAllBe(credentials => credentials == $"{Key}:{Secret}");
        (await client.GetFromJsonAsync<JsonElement[]>("/api/channels"))!.ShouldHaveSingleItem();

        // The authorization is used up.
        (await CompleteAsync(client, started.Id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReceiveKeys_Received_AreEncryptedInTheDatabase()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var started = await StartAsync(client);

        // Act
        await ShopSendsKeysAsync(started.Token);

        // Assert
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ShopAuthorizations.IgnoreQueryFilters()
            .SingleAsync(a => a.Id == started.Id);
        stored.EncryptedCredentials.ShouldNotBeNull();
        stored.EncryptedCredentials.ShouldNotContain(Key);
        stored.EncryptedCredentials.ShouldNotContain(Secret);
        stored.TokenHash.ShouldNotBe(started.Token);
    }

    [Fact]
    public async Task ReceiveKeys_UnknownToken_ReturnsNotFound()
    {
        // Act
        var response = await ShopSendsKeysAsync("nepoznat-token");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReceiveKeys_SecondTime_IsRejectedAndKeepsTheFirstKeys()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAccepts();
        var started = await StartAsync(client);
        await ShopSendsKeysAsync(started.Token);

        // Act
        var response = await ShopSendsKeysAsync(started.Token, "ck_drugi1234567890", "cs_drugi1234567890");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CompleteAsync(client, started.Id)).StatusCode.ShouldBe(HttpStatusCode.Created);
        factory.Shop.Requests.Select(r => Encoding.UTF8.GetString(Convert.FromBase64String(r.Headers.Authorization!.Parameter!)))
            .ShouldAllBe(credentials => credentials == $"{Key}:{Secret}");
    }

    [Fact]
    public async Task ReceiveKeys_NotWooCommerceKeys_IsRejected()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var started = await StartAsync(client);

        // Act
        var response = await ShopSendsKeysAsync(started.Token, Secret, Key);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReceiveKeys_AfterExpiry_IsRejected()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var started = await StartAsync(client);
        await ExpireAsync(started.Id);

        // Act
        var response = await ShopSendsKeysAsync(started.Token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await (await CompleteAsync(client, started.Id)).ReadProblemAsync())
            .GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.authorization_expired");
    }

    [Fact]
    public async Task Complete_BeforeShopSentKeys_ReturnsPending()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var started = await StartAsync(client);

        // Act
        var response = await CompleteAsync(client, started.Id);

        // Assert
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.authorization_pending");
    }

    [Fact]
    public async Task Complete_ShopRejectsKeys_SavesNothingAndCanBeTriedAgain()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var started = await StartAsync(client);
        await ShopSendsKeysAsync(started.Token);
        factory.Shop.RespondWith(_ => throw new HttpRequestException("No such host"));

        // Act
        var failed = await CompleteAsync(client, started.Id);

        // Assert
        (await failed.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.unreachable");
        (await client.GetFromJsonAsync<JsonElement[]>("/api/channels"))!.ShouldBeEmpty();
        ShopAccepts();
        (await CompleteAsync(client, started.Id)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Start_Again_ReplacesTheEarlierAuthorization()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var first = await StartAsync(client);

        // Act
        var second = await StartAsync(client);

        // Assert
        (await ShopSendsKeysAsync(first.Token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CompleteAsync(client, first.Id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ShopSendsKeysAsync(second.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Complete_OtherCompanysAuthorization_ReturnsNotFoundAndKeepsIt()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var other = await factory.CreateSignedInClientAsync();
        ShopAccepts();
        var started = await StartAsync(owner);
        await ShopSendsKeysAsync(started.Token);

        // Act
        var response = await CompleteAsync(other, started.Id);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.GetFromJsonAsync<JsonElement[]>("/api/channels"))!.ShouldBeEmpty();
        (await CompleteAsync(owner, started.Id)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Start_OtherCompanyStarts_DoesNotDropThisCompanysAuthorization()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var other = await factory.CreateSignedInClientAsync();
        var started = await StartAsync(owner);

        // Act
        await StartAsync(other);

        // Assert
        (await ShopSendsKeysAsync(started.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
