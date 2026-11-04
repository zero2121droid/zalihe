using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Receipts;

/// <summary>Receipts with several lines (SPEC 11.7, phase 0): /api/receipts.</summary>
[Collection(ApiCollection.Name)]
public class ReceiptsTests(ZaliheApiFactory factory)
{
    private static async Task<string> CreateItemAsync(HttpClient client, string sku, decimal? purchasePrice = null)
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name = $"Artikal {sku}", sku, unit = "kom", minStock = 0, purchasePrice });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> CreateReceiptAsync(
        HttpClient client, object[] lines, string supplierName = "Kafa Import", string? documentNumber = "F-154", bool allowDuplicate = false) =>
        client.PostAsJsonAsync("/api/receipts", new { supplierName, documentNumber, documentDate = "2026-11-02", lines, allowDuplicate });

    private static async Task<JsonElement> CreatedReceiptAsync(HttpClient client, object[] lines, string supplierName = "Kafa Import", string? documentNumber = "F-154")
    {
        var response = await CreateReceiptAsync(client, lines, supplierName, documentNumber);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> ItemAsync(HttpClient client, string itemId) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}");

    private static async Task<JsonElement> LatestMovementAsync(HttpClient client, string itemId)
    {
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/items/{itemId}/history?filter=stock");
        return history.GetProperty("items")[0].GetProperty("movement");
    }

    [Fact]
    public async Task Create_TwoLines_TakesEverythingIntoStock()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var coffee = await CreateItemAsync(client, "KF-1");
        var filter = await CreateItemAsync(client, "FP-1");

        // Act
        var response = await CreateReceiptAsync(client,
            [new { itemId = coffee, quantity = 24, unitPrice = 1250.5m }, new { itemId = filter, quantity = 2.5m }], " Kafa Import ", " F-154 ");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var receipt = await response.Content.ReadFromJsonAsync<JsonElement>();
        receipt.GetProperty("supplierName").GetString().ShouldBe("Kafa Import");
        receipt.GetProperty("documentNumber").GetString().ShouldBe("F-154");
        receipt.GetProperty("documentDate").GetString().ShouldBe("2026-11-02");
        receipt.GetProperty("createdByName").GetString().ShouldBe("Miljan");
        receipt.GetProperty("lineCount").GetInt32().ShouldBe(2);
        receipt.GetProperty("totalValue").GetDecimal().ShouldBe(30012);
        var lines = receipt.GetProperty("lines").EnumerateArray().ToList();
        lines[0].GetProperty("item").GetProperty("sku").GetString().ShouldBe("KF-1");
        lines[0].GetProperty("unitPrice").GetDecimal().ShouldBe(1250.5m);
        lines[1].GetProperty("unitPrice").ValueKind.ShouldBe(JsonValueKind.Null);
        response.Headers.Location!.ToString().ShouldEndWith($"/api/receipts/{receipt.GetProperty("id").GetString()}");

        (await ItemAsync(client, coffee)).GetProperty("stock").GetDecimal().ShouldBe(24);
        (await ItemAsync(client, filter)).GetProperty("stock").GetDecimal().ShouldBe(2.5m);
        var movement = await LatestMovementAsync(client, coffee);
        movement.GetProperty("type").GetString().ShouldBe("receipt");
        movement.GetProperty("quantity").GetDecimal().ShouldBe(24);
        movement.GetProperty("note").GetString().ShouldBe("Kafa Import");
        movement.GetProperty("externalRef").GetString().ShouldBe("F-154");
        movement.GetProperty("userName").GetString().ShouldBe("Miljan");
    }

    [Fact]
    public async Task Create_UpdatePurchasePrice_ChangesOnlyChosenItems()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var coffee = await CreateItemAsync(client, "KF-1", purchasePrice: 1000);
        var filter = await CreateItemAsync(client, "FP-1", purchasePrice: 300);

        // Act
        await CreatedReceiptAsync(client,
        [
            new { itemId = coffee, quantity = 10, unitPrice = 1100m, updatePurchasePrice = true },
            new { itemId = filter, quantity = 10, unitPrice = 320m, updatePurchasePrice = false },
        ]);

        // Assert
        (await ItemAsync(client, coffee)).GetProperty("purchasePrice").GetDecimal().ShouldBe(1100);
        (await ItemAsync(client, filter)).GetProperty("purchasePrice").GetDecimal().ShouldBe(300);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/items/{coffee}/history");
        var change = history.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("change"))
            .First(c => c.ValueKind == JsonValueKind.Object && c.GetProperty("kind").GetString() == "updated");
        change.GetProperty("changes")[0].GetProperty("field").GetString().ShouldBe("purchasePrice");
        change.GetProperty("changes")[0].GetProperty("newValue").GetString().ShouldBe("1100");
    }

    [Fact]
    public async Task Create_SameDocumentTwice_WarnsUntilAllowed()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var coffee = await CreateItemAsync(client, "KF-1");
        await CreatedReceiptAsync(client, [new { itemId = coffee, quantity = 10 }], "Kafa Import", "F-154");

        // Act
        var again = await CreateReceiptAsync(client, [new { itemId = coffee, quantity = 10 }], "kafa  import", "f-154");
        var allowed = await CreateReceiptAsync(client, [new { itemId = coffee, quantity = 10 }], "kafa  import", "f-154", allowDuplicate: true);
        var otherSupplier = await CreateReceiptAsync(client, [new { itemId = coffee, quantity = 10 }], "Papir d.o.o.", "F-154");
        var noNumber = await CreateReceiptAsync(client, [new { itemId = coffee, quantity = 1 }], "Kafa Import", null);
        var noNumberAgain = await CreateReceiptAsync(client, [new { itemId = coffee, quantity = 1 }], "Kafa Import", null);

        // Assert
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await again.ReadProblemAsync();
        problem.GetProperty("code").GetString().ShouldBe("receipt.duplicate_document");
        problem.GetProperty("errors")[0].GetProperty("params").GetProperty("postedAt").ValueKind.ShouldBe(JsonValueKind.String);
        allowed.StatusCode.ShouldBe(HttpStatusCode.Created);
        otherSupplier.StatusCode.ShouldBe(HttpStatusCode.Created);
        noNumber.StatusCode.ShouldBe(HttpStatusCode.Created);
        noNumberAgain.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ItemAsync(client, coffee)).GetProperty("stock").GetDecimal().ShouldBe(32);
    }

    [Fact]
    public async Task Create_EmployeeWithoutPriceRight_MayReceiveButNotSendPrices()
    {
        // Arrange
        var owner = await factory.CreateSignedInClientAsync();
        var coffee = await CreateItemAsync(owner, "KF-1", purchasePrice: 1000);
        await CreatedReceiptAsync(owner, [new { itemId = coffee, quantity = 5, unitPrice = 1000m }], documentNumber: "F-1");
        var employee = await factory.CreateInvitedClientAsync(owner, "employee");

        // Act
        var withPrice = await CreateReceiptAsync(employee, [new { itemId = coffee, quantity = 1, unitPrice = 1m }], documentNumber: "F-2");
        var withoutPrice = await CreateReceiptAsync(employee, [new { itemId = coffee, quantity = 1 }], documentNumber: "F-3");
        var list = await employee.GetFromJsonAsync<JsonElement>("/api/receipts");

        // Assert
        withPrice.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await withPrice.ReadProblemAsync()).GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("item.purchase_price_not_allowed");
        error.GetProperty("field").GetString().ShouldBe("lines[0].unitPrice");
        withoutPrice.StatusCode.ShouldBe(HttpStatusCode.Created);
        var ownersReceipt = list.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("documentNumber").GetString() == "F-1");
        ownersReceipt.GetProperty("totalValue").ValueKind.ShouldBe(JsonValueKind.Null);
        ownersReceipt.GetProperty("lines")[0].GetProperty("unitPrice").ValueKind.ShouldBe(JsonValueKind.Null);
        (await ItemAsync(owner, coffee)).GetProperty("stock").GetDecimal().ShouldBe(6);
    }

    [Fact]
    public async Task Create_BadLines_ReturnsValidationErrorAndTakesNothing()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var coffee = await CreateItemAsync(client, "KF-1");

        // Act
        var noLines = await CreateReceiptAsync(client, []);
        var zero = await CreateReceiptAsync(client, [new { itemId = coffee, quantity = 1 }, new { itemId = coffee, quantity = 0 }]);
        var noSupplier = await CreateReceiptAsync(client, [new { itemId = coffee, quantity = 1 }], "  ");

        // Assert
        (await noLines.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("field").GetString().ShouldBe("lines");
        var zeroError = (await zero.ReadProblemAsync()).GetProperty("errors")[0];
        zeroError.GetProperty("code").GetString().ShouldBe("validation.positive");
        zeroError.GetProperty("field").GetString().ShouldBe("lines[1].quantity");
        (await noSupplier.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("field").GetString().ShouldBe("supplierName");
        (await ItemAsync(client, coffee)).GetProperty("stock").GetDecimal().ShouldBe(0);
    }

    [Fact]
    public async Task Create_OtherCompanysItem_IsRejected()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var other = await factory.CreateSignedInClientAsync();
        var theirs = await CreateItemAsync(other, "KF-1");

        // Act
        var response = await CreateReceiptAsync(client, [new { itemId = theirs, quantity = 5 }]);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("receipt.item_not_found");
        (await ItemAsync(other, theirs)).GetProperty("stock").GetDecimal().ShouldBe(0);
    }

    [Fact]
    public async Task GetAndList_OtherCompanysReceipt_AreNotVisible()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var other = await factory.CreateSignedInClientAsync();
        var theirs = await CreateItemAsync(other, "KF-1");
        var receiptId = (await CreatedReceiptAsync(other, [new { itemId = theirs, quantity = 5 }])).GetProperty("id").GetString();

        // Act
        var get = await client.GetAsync($"/api/receipts/{receiptId}");
        var list = await client.GetFromJsonAsync<JsonElement>("/api/receipts");

        // Assert
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        list.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task List_Receipts_NewestFirstAndSearchable()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var coffee = await CreateItemAsync(client, "KF-1");
        await CreatedReceiptAsync(client, [new { itemId = coffee, quantity = 1 }], "Kafa Import", "F-154");
        await CreatedReceiptAsync(client, [new { itemId = coffee, quantity = 1 }], "Papir d.o.o.", "OT-77");

        // Act
        var all = await client.GetFromJsonAsync<JsonElement>("/api/receipts");
        var bySupplier = await client.GetFromJsonAsync<JsonElement>("/api/receipts?search=kafa");
        var byNumber = await client.GetFromJsonAsync<JsonElement>("/api/receipts?search=ot-7");

        // Assert
        all.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("documentNumber").GetString()).ShouldBe(["OT-77", "F-154"]);
        bySupplier.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("documentNumber").GetString()).ShouldBe(["F-154"]);
        byNumber.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("documentNumber").GetString()).ShouldBe(["OT-77"]);
    }
}
