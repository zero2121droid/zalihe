using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Imports;

[Collection(ApiCollection.Name)]
public class ItemImportTests(ZaliheApiFactory factory)
{
    private const string Csv =
        "Naziv;Šifra;JM;Nabavna cena;Min. zaliha;Stanje\r\n" +
        "Kafa Etiopija 250 g;KF-ETI-250;kom;900,5;10;12\r\n" +
        "Espresso mešavina;KF-ESP-1000;kg;2.000;5;12,5\r\n" +
        "Šolja keramička;SO-KER-250;kutija;300;8;4\r\n" +
        "Filter papir;FP-V60-100;pak;340;15;\r\n" +
        "Duplikat u fajlu;KF-ETI-250;kom;1;1;1\r\n";

    // Name, Sku, Unit, PurchasePrice, MinStock, InitialStock as suggested for the header above.
    private static readonly Dictionary<string, string> Mapping = new()
    {
        ["nameColumn"] = "0", ["skuColumn"] = "1", ["unitColumn"] = "2",
        ["purchasePriceColumn"] = "3", ["minStockColumn"] = "4", ["initialStockColumn"] = "5",
    };

    private static MultipartFormDataContent Form(byte[] file, Dictionary<string, string>? fields = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(file), "file", "artikli.csv" } };
        foreach (var (key, value) in fields ?? []) form.Add(new StringContent(value), key);
        return form;
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public async Task Analyze_SerbianCsv_ReturnsColumnsSampleAndSuggestedMapping()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PostAsync("/api/imports/items/analyze", Form(Utf8(Csv)));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).ShouldBe(["Naziv", "Šifra", "JM", "Nabavna cena", "Min. zaliha", "Stanje"]);
        body.GetProperty("rowCount").GetInt32().ShouldBe(5);
        body.GetProperty("sampleRows")[0][1].GetString().ShouldBe("KF-ETI-250");
        var mapping = body.GetProperty("suggestedMapping");
        mapping.GetProperty("name").GetInt32().ShouldBe(0);
        mapping.GetProperty("sku").GetInt32().ShouldBe(1);
        mapping.GetProperty("initialStock").GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task Preview_MixedRows_CountsReadyErrorsAndListsIssues()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PostAsync("/api/imports/items/preview", Form(Utf8(Csv), Mapping));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("readyCount").GetInt32().ShouldBe(3);
        body.GetProperty("errorCount").GetInt32().ShouldBe(2);
        body.GetProperty("skippedCount").GetInt32().ShouldBe(0);
        var issues = body.GetProperty("issues").EnumerateArray()
            .Select(i => (i.GetProperty("rowNumber").GetInt32(), i.GetProperty("errors")[0].GetProperty("code").GetString()))
            .ToList();
        issues.ShouldBe([(4, "import.unit_unknown"), (6, "import.sku_duplicate_in_file")]);
    }

    [Fact]
    public async Task Import_MixedRows_CreatesValidItemsWithOpeningStock()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();

        // Act
        var response = await client.PostAsync("/api/imports/items", Form(Utf8(Csv), Mapping));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("importedCount").GetInt32().ShouldBe(3);

        var items = (await client.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("sku").GetString()!);
        items.Keys.ShouldBe(["KF-ESP-1000", "FP-V60-100", "KF-ETI-250"], ignoreOrder: true);
        items["KF-ESP-1000"].GetProperty("stock").GetDecimal().ShouldBe(12.5m);
        items["KF-ESP-1000"].GetProperty("unit").GetString().ShouldBe("kg");
        // "2.000" in a ";"-separated file is two thousand (Serbian Excel), not two.
        items["KF-ESP-1000"].GetProperty("purchasePrice").GetDecimal().ShouldBe(2000);
        items["KF-ETI-250"].GetProperty("purchasePrice").GetDecimal().ShouldBe(900.5m);
        items["FP-V60-100"].GetProperty("stock").GetDecimal().ShouldBe(0);

        var id = items["KF-ESP-1000"].GetProperty("id").GetString();
        var history = (await client.GetFromJsonAsync<JsonElement>($"/api/items/{id}/history")).GetProperty("items").EnumerateArray().ToList();
        var opening = history.Single(e => e.GetProperty("movement").ValueKind != JsonValueKind.Null).GetProperty("movement");
        opening.GetProperty("type").GetString().ShouldBe("adjustment");
        opening.GetProperty("source").GetString().ShouldBe("csv");
        opening.GetProperty("note").GetString().ShouldBe("Početno stanje");
        history.ShouldContain(e => e.GetProperty("change").ValueKind != JsonValueKind.Null
            && e.GetProperty("change").GetProperty("kind").GetString() == "created");
    }

    [Fact]
    public async Task Import_SkuAlreadyExists_SkipsRowAndLeavesItemUnchanged()
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        await client.PostAsJsonAsync("/api/items", new { name = "Postojeća kafa", sku = "KF-ETI-250", unit = "kom", minStock = 1 });

        // Act
        var preview = await (await client.PostAsync("/api/imports/items/preview", Form(Utf8(Csv), Mapping))).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsync("/api/imports/items", Form(Utf8(Csv), Mapping));

        // Assert
        // The file has KF-ETI-250 in rows 2 and 6; with the item already existing, both are skipped.
        preview.GetProperty("skippedCount").GetInt32().ShouldBe(2);
        var skipped = preview.GetProperty("issues").EnumerateArray().Where(i => i.GetProperty("skipped").GetBoolean()).ToList();
        skipped.Select(i => i.GetProperty("rowNumber").GetInt32()).ShouldBe([2, 6]);
        skipped.ShouldAllBe(i => i.GetProperty("sku").GetString() == "KF-ETI-250"
            && i.GetProperty("errors")[0].GetProperty("code").GetString() == "import.sku_exists");

        var existing = (await client.GetFromJsonAsync<JsonElement>("/api/items?search=KF-ETI-250")).GetProperty("items").EnumerateArray().Single();
        existing.GetProperty("name").GetString().ShouldBe("Postojeća kafa");
        existing.GetProperty("stock").GetDecimal().ShouldBe(0);
    }

    [Fact]
    public async Task Preview_SameSkuInOtherCompany_IsNotTreatedAsExisting()
    {
        // Arrange
        var companyA = await factory.CreateSignedInClientAsync();
        var companyB = await factory.CreateSignedInClientAsync();
        await companyA.PostAsJsonAsync("/api/items", new { name = "Kafa firme A", sku = "KF-ETI-250", unit = "kom" });

        // Act
        var preview = await (await companyB.PostAsync("/api/imports/items/preview", Form(Utf8(Csv), Mapping))).Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        preview.GetProperty("skippedCount").GetInt32().ShouldBe(0);
        preview.GetProperty("readyCount").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Import_Windows1250File_KeepsSerbianLetters()
    {
        // Arrange
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var client = await factory.CreateSignedInClientAsync();
        var file = Encoding.GetEncoding(1250).GetBytes("Naziv;Šifra\r\nČajnik stakleni đak;CA-STA-600\r\n");

        // Act
        await client.PostAsync("/api/imports/items", Form(file, new() { ["nameColumn"] = "0", ["skuColumn"] = "1" }));

        // Assert
        var item = (await client.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("items")[0];
        item.GetProperty("name").GetString().ShouldBe("Čajnik stakleni đak");
    }

    [Theory]
    [InlineData("excel")]
    [InlineData("empty")]
    [InlineData("nomapping")]
    public async Task Preview_BadFileOrMapping_ReturnsFileError(string kind)
    {
        // Arrange
        var client = await factory.CreateSignedInClientAsync();
        var (file, fields, code) = kind switch
        {
            "excel" => (new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 }, Mapping, "import.excel_not_supported"),
            "empty" => (Utf8("\r\n\r\n"), Mapping, "import.file_empty"),
            _ => (Utf8(Csv), new Dictionary<string, string> { ["nameColumn"] = "0" }, "import.mapping_invalid"),
        };

        // Act
        var response = await client.PostAsync("/api/imports/items/preview", Form(file, fields));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("code").GetString().ShouldBe(code);
        error.GetProperty("field").GetString().ShouldBe("file");
    }

    [Fact]
    public async Task Import_WithoutAntiforgeryToken_IsRejected()
    {
        // Arrange
        var client = factory.CreateClient();
        await client.RegisterAsync(HttpClientExtensions.UniqueEmail());

        // Act
        var response = await client.PostAsync("/api/imports/items", Form(Utf8(Csv), Mapping));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).GetProperty("code").GetString().ShouldBe("auth.antiforgery_invalid");
    }
}
