using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;
using static Zalihe.IntegrationTests.Infrastructure.HttpClientExtensions;

namespace Zalihe.IntegrationTests.Platform;

/// <summary>The page about all companies (SPEC 11.8): /api/platform.</summary>
[Collection(ApiCollection.Name)]
public class PlatformTests(ZaliheApiFactory factory)
{
    private const string Password = "sigurna-lozinka";
    private const string OwnerCompany = "Firma vlasnika platforme";

    /// <summary>Signs in as the platform's owner, whose company the first test to need it registers.</summary>
    private async Task<HttpClient> OwnerAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = ZaliheApiFactory.PlatformOwnerEmail, password = Password });
        if (login.StatusCode != HttpStatusCode.NoContent)
        {
            await client.RegisterAsync(factory, ZaliheApiFactory.PlatformOwnerEmail, Password, OwnerCompany, "Vlasnik");
        }

        return client;
    }

    /// <summary>Registers a company with a name no other test uses and returns the name with its signed-in admin.</summary>
    private async Task<(string Name, HttpClient Client)> CompanyAsync()
    {
        var name = $"Firma {Guid.NewGuid():N}";
        var client = factory.CreateClient();
        await client.RegisterAsync(factory, UniqueEmail(), Password, name, "Ana");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", await client.GetAntiforgeryTokenAsync());
        return (name, client);
    }

    private static async Task<JsonElement> FindAsync(HttpClient owner, string company)
    {
        var page = await owner.GetFromJsonAsync<JsonElement>($"/api/platform/companies?search={company}");
        return page.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
    }

    private static Task<JsonElement> SummaryAsync(HttpClient owner) =>
        owner.GetFromJsonAsync<JsonElement>("/api/platform/summary");

    [Theory]
    [InlineData("/api/platform/summary")]
    [InlineData("/api/platform/companies")]
    public async Task Get_CompanyAdmin_IsForbidden(string path)
    {
        // Arrange
        var (_, admin) = await CompanyAsync();

        // Act
        var response = await admin.GetAsync(path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.forbidden");
    }

    [Theory]
    [InlineData("/api/platform/summary")]
    [InlineData("/api/platform/companies")]
    public async Task Get_NotSignedIn_IsUnauthorized(string path)
    {
        // Act
        var response = await factory.CreateClient().GetAsync(path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Companies_NewCompany_ShowsItsCountsWithoutItsData()
    {
        // Arrange
        var owner = await OwnerAsync();
        var (name, admin) = await CompanyAsync();
        var created = await admin.PostAsJsonAsync("/api/items", new { name = "Tajni artikal", sku = "TAJNA-1", unit = "kom", minStock = 0 });
        var itemId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        (await admin.PostAsJsonAsync("/api/receipts", new
        {
            supplierName = "Dobavljač",
            documentNumber = "F-1",
            documentDate = "2026-11-02",
            lines = new object[] { new { itemId, quantity = 5 } },
        })).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        var company = await FindAsync(owner, name);

        // Assert
        company.GetProperty("activeItems").GetInt32().ShouldBe(1);
        company.GetProperty("hasShop").GetBoolean().ShouldBeFalse();
        company.GetProperty("lastMovementAt").ValueKind.ShouldBe(JsonValueKind.String);
        company.GetProperty("isOwn").GetBoolean().ShouldBeFalse();
        company.GetRawText().ShouldNotContain("Tajni artikal");
    }

    [Fact]
    public async Task Companies_EmptyCompany_HasNoLastMovement()
    {
        // Arrange
        var owner = await OwnerAsync();
        var (name, _) = await CompanyAsync();

        // Act
        var company = await FindAsync(owner, name);

        // Assert
        company.GetProperty("activeItems").GetInt32().ShouldBe(0);
        company.GetProperty("lastMovementAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Companies_OwnersCompany_IsListedAndMarked()
    {
        // Arrange
        var owner = await OwnerAsync();

        // Act: the search ignores case
        var company = await FindAsync(owner, "VLASNIKA%20PLATFORME");

        // Assert
        company.GetProperty("isOwn").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Summary_NewCompany_CountsItButNotTheOwnersCompany()
    {
        // Arrange
        var owner = await OwnerAsync();
        var before = await SummaryAsync(owner);
        var ownCompanies = (await owner.GetFromJsonAsync<JsonElement>("/api/platform/companies?pageSize=100"))
            .GetProperty("totalCount").GetInt32() - before.GetProperty("companies").GetInt32();

        // Act
        await CompanyAsync();
        var after = await SummaryAsync(owner);

        // Assert
        after.GetProperty("companies").GetInt32().ShouldBe(before.GetProperty("companies").GetInt32() + 1);
        after.GetProperty("newLast7Days").GetInt32().ShouldBe(before.GetProperty("newLast7Days").GetInt32() + 1);
        ownCompanies.ShouldBe(1);
    }

    [Fact]
    public async Task Summary_MovementInACompany_CountsAsActive()
    {
        // Arrange
        var owner = await OwnerAsync();
        var (_, admin) = await CompanyAsync();
        var before = await SummaryAsync(owner);
        var created = await admin.PostAsJsonAsync("/api/items", new { name = "Artikal", sku = "A-1", unit = "kom", minStock = 0 });
        var itemId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // Act
        (await admin.PostAsJsonAsync("/api/receipts", new
        {
            supplierName = "Dobavljač",
            documentNumber = "F-2",
            documentDate = "2026-11-02",
            lines = new object[] { new { itemId, quantity = 1 } },
        })).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Assert
        var after = await SummaryAsync(owner);
        after.GetProperty("activeLast7Days").GetInt32().ShouldBe(before.GetProperty("activeLast7Days").GetInt32() + 1);
    }

    [Fact]
    public async Task Me_Owner_SaysSoOnlyToTheOwner()
    {
        // Arrange
        var owner = await OwnerAsync();
        var (_, admin) = await CompanyAsync();

        // Act
        var ownerMe = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var adminMe = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me");

        // Assert
        ownerMe.GetProperty("isPlatformOwner").GetBoolean().ShouldBeTrue();
        adminMe.GetProperty("isPlatformOwner").GetBoolean().ShouldBeFalse();
    }
}
