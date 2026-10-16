using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Application.Common;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Channels;

[Collection(ApiCollection.Name)]
public class ChannelsTests(ZaliheApiFactory factory)
{
    private const string Key = "ck_1234567890abcdef";
    private const string Secret = "cs_1234567890abcdef";

    private static object Connect(string baseUrl = "zrno.rs/", string key = Key, string secret = Secret) =>
        new { baseUrl, consumerKey = key, consumerSecret = secret };

    private void ShopAnswers(HttpStatusCode status, string body = "[]") =>
        factory.Shop.RespondWith(_ => FakeShopHandler.Json(body, status));

    [Fact]
    public async Task Connect_WorkingKeys_SavesChannelAndCallsShopWithBasicAuth()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAnswers(HttpStatusCode.OK, "[{\"id\":10}]");

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce", Connect());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var channel = await response.Content.ReadFromJsonAsync<JsonElement>();
        channel.GetProperty("baseUrl").GetString().ShouldBe("https://zrno.rs");
        channel.GetProperty("status").GetString().ShouldBe("connected");
        channel.TryGetProperty("encryptedCredentials", out _).ShouldBeFalse();

        factory.Shop.Requests[0].RequestUri!.ToString().ShouldStartWith("https://zrno.rs/wp-json/wc/v3/products");
        factory.Shop.Requests.ShouldAllBe(r => r.Headers.Authorization!.Scheme == "Basic");
        factory.Shop.Requests.Select(r => Encoding.UTF8.GetString(Convert.FromBase64String(r.Headers.Authorization!.Parameter!)))
            .ShouldAllBe(credentials => credentials == $"{Key}:{Secret}");

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/channels");
        list!.ShouldHaveSingleItem().GetProperty("baseUrl").GetString().ShouldBe("https://zrno.rs");
    }

    [Fact]
    public async Task Connect_SavedKeys_AreEncryptedInTheDatabase()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAnswers(HttpStatusCode.OK);

        // Act
        var created = await (await client.PostAsJsonAsync("/api/channels/woocommerce", Connect())).Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.Parse(created.GetProperty("id").GetString()!);
        var stored = await db.SalesChannels.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        stored.EncryptedCredentials.ShouldNotContain(Key);
        stored.EncryptedCredentials.ShouldNotContain(Secret);
        stored.EncryptedWebhookSecret.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "channel.unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "channel.unauthorized")]
    [InlineData(HttpStatusCode.NotFound, "channel.not_woocommerce")]
    [InlineData(HttpStatusCode.InternalServerError, "channel.unexpected_response")]
    public async Task Connect_ShopRejects_ReturnsReasonAndSavesNothing(HttpStatusCode status, string code)
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAnswers(status, "{}");

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce", Connect());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe(code);
        (await client.GetFromJsonAsync<JsonElement[]>("/api/channels"))!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Connect_SecurityPluginAnswersWithHtml_ReturnsUnexpectedResponse()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        factory.Shop.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Access denied</body></html>", Encoding.UTF8, "text/html"),
        });

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce", Connect());

        // Assert
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.unexpected_response");
    }

    [Fact]
    public async Task Connect_ShopUnreachable_ReturnsUnreachable()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        factory.Shop.RespondWith(_ => throw new HttpRequestException("No such host"));

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce", Connect());

        // Assert
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.unreachable");
    }

    [Theory]
    [InlineData("http://zrno.rs", Key, Secret, "channel.url_invalid", "baseUrl")]
    [InlineData("zrno.rs", Secret, Key, "channel.keys_invalid", "consumerKey")]
    [InlineData("zrno.rs", "ck_", "cs_", "channel.keys_invalid", "consumerKey")]
    public async Task Connect_BadInput_ReturnsFieldErrorWithoutCallingShop(string url, string key, string secret, string code, string field)
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAnswers(HttpStatusCode.OK);

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce", Connect(url, key, secret));

        // Assert
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe(code);
        error.GetProperty("field").GetString().ShouldBe(field);
        factory.Shop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Connect_SecondShop_ReturnsAlreadyConnected()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAnswers(HttpStatusCode.OK);
        await client.PostAsJsonAsync("/api/channels/woocommerce", Connect());

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce", Connect("drugi.rs"));

        // Assert
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.already_connected");
    }

    [Fact]
    public async Task Check_KeysRevokedLater_MarksErrorAndNewKeysFixIt()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAnswers(HttpStatusCode.OK);
        var id = (await (await client.PostAsJsonAsync("/api/channels/woocommerce", Connect())).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();

        // Act: the shop owner revoked the keys
        ShopAnswers(HttpStatusCode.Unauthorized, "{}");
        var failed = await (await client.PostAsync($"/api/channels/{id}/check", null)).Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        failed.GetProperty("status").GetString().ShouldBe("error");
        failed.GetProperty("lastErrorCode").GetString().ShouldBe("channel.unauthorized");

        // Act: new keys that work
        ShopAnswers(HttpStatusCode.OK);
        var fixedChannel = await (await client.PutAsJsonAsync($"/api/channels/{id}/credentials",
            new { consumerKey = "ck_new1234567890", consumerSecret = "cs_new1234567890" })).Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        fixedChannel.GetProperty("status").GetString().ShouldBe("connected");
        fixedChannel.GetProperty("lastErrorCode").ValueKind.ShouldBe(JsonValueKind.Null);
        factory.Shop.Requests.Select(r => Encoding.UTF8.GetString(Convert.FromBase64String(r.Headers.Authorization!.Parameter!)))
            .ShouldAllBe(credentials => credentials == "ck_new1234567890:cs_new1234567890");
    }

    private async Task<(Guid Id, string Secret)> StoredChannelAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var channel = await services.GetRequiredService<AppDbContext>().SalesChannels.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        return (id, services.GetRequiredService<ICredentialProtector>().Unprotect(channel.EncryptedWebhookSecret));
    }

    private List<JsonElement> SentWebhooks(HttpMethod method) =>
        factory.Shop.Requests
            .Select((request, i) => (request, body: factory.Shop.Bodies[i]))
            .Where(r => r.request.Method == method && r.request.RequestUri!.AbsolutePath.Contains("/webhooks", StringComparison.Ordinal))
            .Select(r => JsonDocument.Parse(r.body!).RootElement.Clone())
            .ToList();

    [Fact]
    public async Task Connect_WorkingKeys_RegistersOrderWebhooksSignedWithChannelSecret()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAnswers(HttpStatusCode.OK);

        // Act
        var created = await (await client.PostAsJsonAsync("/api/channels/woocommerce", Connect())).Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        var (id, secret) = await StoredChannelAsync(created.GetProperty("id").GetGuid());
        var webhooks = SentWebhooks(HttpMethod.Post);
        webhooks.Select(w => w.GetProperty("topic").GetString()).ShouldBe(["order.created", "order.updated"]);
        webhooks.ShouldAllBe(w => w.GetProperty("delivery_url").GetString() == $"{ZaliheApiFactory.WebhookBaseUrl}/webhooks/woo/{id}");
        webhooks.ShouldAllBe(w => w.GetProperty("secret").GetString() == secret);
        webhooks.ShouldAllBe(w => w.GetProperty("status").GetString() == "active");
    }

    [Fact]
    public async Task Check_WebhooksAlreadyInShop_UpdatesThemInsteadOfAddingMore()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        ShopAnswers(HttpStatusCode.OK);
        var id = (await (await client.PostAsJsonAsync("/api/channels/woocommerce", Connect())).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var url = $"{ZaliheApiFactory.WebhookBaseUrl}/webhooks/woo/{id}";
        // order.created is disabled after failed deliveries; another plugin's webhook is left alone
        var existing = JsonSerializer.Serialize(new object[]
        {
            new { id = 7, topic = "order.created", delivery_url = url, status = "disabled" },
            new { id = 8, topic = "order.updated", delivery_url = url, status = "active" },
            new { id = 9, topic = "order.created", delivery_url = "https://other.example/hook", status = "active" },
        });
        factory.Shop.RespondWith(request => FakeShopHandler.Json(
            request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/webhooks", StringComparison.Ordinal)
            && request.RequestUri.Query.Contains("page=1", StringComparison.Ordinal) ? existing : "[]"));

        // Act
        var checkedChannel = await (await client.PostAsync($"/api/channels/{id}/check", null)).Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        checkedChannel.GetProperty("status").GetString().ShouldBe("connected");
        SentWebhooks(HttpMethod.Post).ShouldBeEmpty();
        factory.Shop.Requests.Where(r => r.Method == HttpMethod.Put).Select(r => r.RequestUri!.AbsolutePath)
            .ShouldBe(["/wp-json/wc/v3/webhooks/7", "/wp-json/wc/v3/webhooks/8"]);
        SentWebhooks(HttpMethod.Put).ShouldAllBe(w => w.GetProperty("status").GetString() == "active");
    }

    [Fact]
    public async Task Connect_ReadOnlyKeys_ReturnsUnauthorizedAndSavesNothing()
    {
        // Arrange: reading works, but creating a webhook needs read/write keys
        var client = await factory.CreateSignedInClientAsync();
        factory.Shop.RespondWith(request => request.Method == HttpMethod.Get
            ? FakeShopHandler.Json("[]")
            : FakeShopHandler.Json("""{"code":"woocommerce_rest_cannot_create"}""", HttpStatusCode.Unauthorized));

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/woocommerce", Connect());

        // Assert
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("channel.unauthorized");
        (await client.GetFromJsonAsync<JsonElement[]>("/api/channels"))!.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAndCheck_ChannelOfOtherCompany_NotVisible()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        ShopAnswers(HttpStatusCode.OK);
        var id = (await (await companyA.PostAsJsonAsync("/api/channels/woocommerce", Connect())).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();

        // Act
        var list = await companyB.GetFromJsonAsync<JsonElement[]>("/api/channels");
        var check = await companyB.PostAsync($"/api/channels/{id}/check", null);

        // Assert
        list!.ShouldBeEmpty();
        check.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
