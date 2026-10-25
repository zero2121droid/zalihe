using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Auth;

/// <summary>
/// Purchase prices and what is computed from them, by role and the company's setting (SPEC 11.2):
/// an employee without the right gets them from no endpoint, can't send them and can't sort by them.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchasePriceTests(ZaliheApiFactory factory)
{
    private record Company(HttpClient Owner, HttpClient Member, Guid ItemId);

    /// <summary>A company with an item that has a purchase price and stock, and a member with <paramref name="role"/>.</summary>
    private async Task<Company> CompanyAsync(string role, bool employeesSeePrices = false)
    {
        var owner = await factory.CreateSignedInClientAsync();
        var created = await owner.PostAsJsonAsync("/api/items",
            new { name = "Kafa", sku = "KF-1", unit = "kom", minStock = 0, purchasePrice = 800m, salePrice = 1200m });
        var id = Guid.Parse((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
        (await owner.PostAsJsonAsync($"/api/items/{id}/movements", new { kind = "receipt", quantity = 5 })).EnsureSuccessStatusCode();
        await SetEmployeesSeePricesAsync(owner, employeesSeePrices);
        return new Company(owner, await factory.CreateInvitedClientAsync(owner, role), id);
    }

    private static async Task SetEmployeesSeePricesAsync(HttpClient admin, bool value) =>
        (await admin.PutAsJsonAsync("/api/account/company/purchase-prices", new { employeesSeePurchasePrices = value }))
            .EnsureSuccessStatusCode();

    private static async Task<JsonElement> ItemAsync(HttpClient client, Guid id) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/items/{id}");

    private static bool IsNull(JsonElement element, string property) => element.GetProperty(property).ValueKind == JsonValueKind.Null;

    [Theory]
    [InlineData("employee", false, false)]
    [InlineData("employee", true, true)]
    [InlineData("manager", false, true)]
    [InlineData("admin", false, true)]
    public async Task Read_ByRoleAndSetting_PricesAndValuesOnlyForThoseAllowed(string role, bool employeesSeePrices, bool sees)
    {
        // Arrange
        var company = await CompanyAsync(role, employeesSeePrices);

        // Act
        var item = await ItemAsync(company.Member, company.ItemId);
        var listed = (await company.Member.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("items")[0];
        var dashboard = await company.Member.GetFromJsonAsync<JsonElement>("/api/dashboard");
        var me = await company.Member.GetFromJsonAsync<JsonElement>("/api/auth/me");

        // Assert
        foreach (var dto in new[] { item, listed })
        {
            IsNull(dto, "purchasePrice").ShouldBe(!sees);
            IsNull(dto, "stockValue").ShouldBe(!sees);
            dto.GetProperty("salePrice").GetDecimal().ShouldBe(1200m);
        }

        IsNull(dashboard, "stockValue").ShouldBe(!sees);
        if (sees) dashboard.GetProperty("stockValue").GetDecimal().ShouldBe(4000m);
        me.GetProperty("canSeePurchasePrices").GetBoolean().ShouldBe(sees);
    }

    [Fact]
    public async Task Setting_TurnedOnAndOff_AppliesAtTheEmployeesNextRequest()
    {
        // Arrange
        var company = await CompanyAsync("employee");
        IsNull(await ItemAsync(company.Member, company.ItemId), "purchasePrice").ShouldBeTrue();

        // Act & Assert: no signing in again either way.
        await SetEmployeesSeePricesAsync(company.Owner, true);
        (await ItemAsync(company.Member, company.ItemId)).GetProperty("purchasePrice").GetDecimal().ShouldBe(800m);

        await SetEmployeesSeePricesAsync(company.Owner, false);
        IsNull(await ItemAsync(company.Member, company.ItemId), "purchasePrice").ShouldBeTrue();
    }

    [Fact]
    public async Task Setting_ChangedByManager_IsForbidden()
    {
        // Arrange
        var company = await CompanyAsync("manager");

        // Act
        var response = await company.Member.PutAsJsonAsync("/api/account/company/purchase-prices", new { employeesSeePurchasePrices = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_EmployeeSortsByValue_IsRefused()
    {
        // Arrange
        var company = await CompanyAsync("employee");

        // Act
        var response = await company.Member.GetAsync("/api/items?sort=value&direction=desc");

        // Assert
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("item.sort_not_allowed");
        error.GetProperty("field").GetString().ShouldBe("sort");
        (await company.Owner.GetAsync("/api/items?sort=value&direction=desc")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_EmployeeSendsPurchasePrice_IsRefused()
    {
        // Arrange
        var company = await CompanyAsync("employee");

        // Act
        var response = await company.Member.PostAsJsonAsync("/api/items",
            new { name = "Čaj", sku = "CJ-1", unit = "kom", minStock = 0, purchasePrice = 300m });

        // Assert
        var error = (await response.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("item.purchase_price_not_allowed");
        error.GetProperty("field").GetString().ShouldBe("purchasePrice");
    }

    [Fact]
    public async Task Update_EmployeeEditsItem_KeepsThePurchasePriceTheyDontSee()
    {
        // Arrange
        var company = await CompanyAsync("employee");

        // Act
        var response = await company.Member.PutAsJsonAsync($"/api/items/{company.ItemId}",
            new { name = "Kafa Etiopija", sku = "KF-1", unit = "kom", minStock = 2, salePrice = 1300m });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var seenByOwner = await ItemAsync(company.Owner, company.ItemId);
        seenByOwner.GetProperty("name").GetString().ShouldBe("Kafa Etiopija");
        seenByOwner.GetProperty("purchasePrice").GetDecimal().ShouldBe(800m);
        seenByOwner.GetProperty("salePrice").GetDecimal().ShouldBe(1300m);
    }

    [Fact]
    public async Task History_Employee_GetsNoPurchasePriceChanges()
    {
        // Arrange: one edit of the purchase price only, one of the price and the name.
        var company = await CompanyAsync("employee");
        await company.Owner.PutAsJsonAsync($"/api/items/{company.ItemId}",
            new { name = "Kafa", sku = "KF-1", unit = "kom", minStock = 0, purchasePrice = 850m, salePrice = 1200m });
        await company.Owner.PutAsJsonAsync($"/api/items/{company.ItemId}",
            new { name = "Kafa Etiopija", sku = "KF-1", unit = "kom", minStock = 0, purchasePrice = 900m, salePrice = 1200m });

        // Act
        var forEmployee = await company.Member.GetFromJsonAsync<JsonElement>($"/api/items/{company.ItemId}/history?filter=changes");
        var forOwner = await company.Owner.GetFromJsonAsync<JsonElement>($"/api/items/{company.ItemId}/history?filter=changes");

        // Assert
        static IEnumerable<string> Fields(JsonElement page) => page.GetProperty("items").EnumerateArray()
            .SelectMany(e => e.GetProperty("change").GetProperty("changes").EnumerateArray())
            .Select(f => f.GetProperty("field").GetString()!);
        Fields(forOwner).Count(f => f == "purchasePrice").ShouldBe(2);
        Fields(forEmployee).ShouldBe(["name"]);
        forEmployee.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("change").GetProperty("kind").GetString())
            .ShouldBe(["updated", "created"]);
    }
}
