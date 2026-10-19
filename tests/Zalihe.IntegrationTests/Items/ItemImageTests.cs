using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Items;

[Collection(ApiCollection.Name)]
public class ItemImageTests(ZaliheApiFactory factory)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, .. "JFIF"u8, 0x00, 0x01];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private static async Task<string> CreateItemAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name = "Kafa Etiopija 250 g", sku = "KF-ETI-250", unit = "kom", minStock = 0 });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string itemId, byte[] data, string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", "slika.jpg" } };
        return client.PutAsync($"/api/items/{itemId}/image", form);
    }

    private static async Task<JsonElement> ItemAsync(HttpClient client, string itemId) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}");

    [Fact]
    public async Task SetImage_Jpeg_ItemShowsItAndTheApiServesIt()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        var response = await UploadAsync(client, itemId, Jpeg);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        item.GetProperty("hasOwnImage").GetBoolean().ShouldBeTrue();
        var url = item.GetProperty("imageUrl").GetString()!;
        url.ShouldStartWith($"/api/items/{itemId}/image?v=");

        var image = await client.GetAsync(url);
        image.StatusCode.ShouldBe(HttpStatusCode.OK);
        image.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        (await image.Content.ReadAsByteArrayAsync()).ShouldBe(Jpeg);
        image.Headers.CacheControl!.Private.ShouldBeTrue();
        image.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    [Fact]
    public async Task SetImage_TypeClaimedByTheClient_IsIgnoredForTheContent()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act: a PNG sent as "image/jpeg" is served as what it is.
        await UploadAsync(client, itemId, Png, "image/jpeg");

        // Assert
        var url = (await ItemAsync(client, itemId)).GetProperty("imageUrl").GetString()!;
        (await client.GetAsync(url)).Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
    }

    [Fact]
    public async Task SetImage_Again_ReplacesItUnderANewAddress()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);
        var first = (await (await UploadAsync(client, itemId, Jpeg)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imageUrl").GetString();
        await Task.Delay(5);

        // Act
        var second = (await (await UploadAsync(client, itemId, Png)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imageUrl").GetString()!;

        // Assert
        second.ShouldNotBe(first);
        (await client.GetByteArrayAsync(second)).ShouldBe(Png);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "image/svg+xml")]
    [InlineData("GIF89a....", "image/gif")]
    [InlineData("nije slika", "image/jpeg")]
    public async Task SetImage_NotAJpegPngOrWebP_Returns400WithCode(string content, string contentType)
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        var response = await UploadAsync(client, itemId, System.Text.Encoding.UTF8.GetBytes(content), contentType);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("item.image_unsupported");
        problem.GetProperty("errors")[0].GetProperty("field").GetString().ShouldBe("file");
        (await ItemAsync(client, itemId)).GetProperty("imageUrl").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SetImage_LargerThan2Mb_Returns400WithLimit()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        var response = await UploadAsync(client, itemId, [.. Jpeg, .. new byte[2 * 1024 * 1024]]);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("item.image_too_large");
        error.GetProperty("params").GetProperty("maxMb").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task SetImage_WithoutFile_Returns400()
    {
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        var response = await client.PutAsync($"/api/items/{itemId}/image", new MultipartFormDataContent { { new StringContent("x"), "other" } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveImage_AfterSetting_ItemHasNoPictureAndTheAddressIsGone()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);
        var url = (await (await UploadAsync(client, itemId, Jpeg)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imageUrl").GetString()!;

        // Act
        var response = await client.DeleteAsync($"/api/items/{itemId}/image");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var item = await ItemAsync(client, itemId);
        item.GetProperty("imageUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        item.GetProperty("hasOwnImage").GetBoolean().ShouldBeFalse();
        (await client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Image_OfAnotherCompanysItem_IsNotFoundForEveryAction()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(owner);
        await UploadAsync(owner, itemId, Jpeg);
        var other = await factory.CreateSignedInClientAsync();

        // Act & Assert
        (await other.GetAsync($"/api/items/{itemId}/image")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await UploadAsync(other, itemId, Png)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/items/{itemId}/image")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await owner.GetByteArrayAsync((await ItemAsync(owner, itemId)).GetProperty("imageUrl").GetString())).ShouldBe(Jpeg);
    }

    [Fact]
    public async Task Image_Anonymous_Returns401()
    {
        var owner = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(owner);
        await UploadAsync(owner, itemId, Jpeg);

        (await factory.CreateClient().GetAsync($"/api/items/{itemId}/image")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetImage_CookieWithoutAntiforgeryToken_IsRefused()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        // Act
        var response = await UploadAsync(client, itemId, Jpeg);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.antiforgery_invalid");
        (await ItemAsync(client, itemId)).GetProperty("imageUrl").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SetImage_LargerThanTheRequestLimit_IsRefusedAsInvalid()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await CreateItemAsync(client);

        // Act
        var response = await UploadAsync(client, itemId, [.. Jpeg, .. new byte[5 * 1024 * 1024]]);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("validation.failed");
    }
}
