using Shouldly;
using Zalihe.Application.Imports;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Tests.Imports;

public class ItemImportRulesTests
{
    private static readonly Dictionary<ItemImportField, int> Mapping = new()
    {
        [ItemImportField.Name] = 0,
        [ItemImportField.Sku] = 1,
        [ItemImportField.Unit] = 2,
        [ItemImportField.PurchasePrice] = 3,
        [ItemImportField.MinStock] = 4,
        [ItemImportField.InitialStock] = 5,
    };

    private static ImportRowResult Read(params string[] cells) =>
        ItemImportRules.ReadRow(new CsvRow(7, cells), Mapping, Unit.Kom);

    [Fact]
    public void SuggestMapping_SerbianHeaders_MapsByName()
    {
        // Act
        var mapping = ItemImportRules.SuggestMapping(
            ["Naziv artikla", "Šifra", "JM", "Nabavna cena", "Prodajna cena", "Min. zaliha", "Stanje", "Napomena"]);

        // Assert
        mapping.ShouldBe(new Dictionary<ItemImportField, int>
        {
            [ItemImportField.Name] = 0,
            [ItemImportField.Sku] = 1,
            [ItemImportField.Unit] = 2,
            [ItemImportField.PurchasePrice] = 3,
            [ItemImportField.SalePrice] = 4,
            [ItemImportField.MinStock] = 5,
            [ItemImportField.InitialStock] = 6,
        }, ignoreOrder: true);
    }

    [Fact]
    public void SuggestMapping_EnglishHeaders_MapsByName()
    {
        ItemImportRules.SuggestMapping(["SKU", "Product name", "Quantity", "Price"]).ShouldBe(new Dictionary<ItemImportField, int>
        {
            [ItemImportField.Sku] = 0,
            [ItemImportField.Name] = 1,
            [ItemImportField.InitialStock] = 2,
            [ItemImportField.SalePrice] = 3,
        }, ignoreOrder: true);
    }

    [Theory]
    [InlineData("kom", Unit.Kom)]
    [InlineData("Kom.", Unit.Kom)]
    [InlineData("komada", Unit.Kom)]
    [InlineData("KG", Unit.Kg)]
    [InlineData("gr", Unit.G)]
    [InlineData("lit", Unit.L)]
    [InlineData("pakovanje", Unit.Pak)]
    public void ParseUnit_KnownSpelling_ReturnsUnit(string value, Unit expected)
    {
        ItemImportRules.ParseUnit(value).ShouldBe(expected);
    }

    [Fact]
    public void ParseUnit_Unknown_ReturnsNull()
    {
        ItemImportRules.ParseUnit("kutija").ShouldBeNull();
    }

    [Theory]
    [InlineData("12,5", 12.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("1.284,50", 1284.5)]
    [InlineData("1,284.50", 1284.5)]
    [InlineData("1 284,5", 1284.5)]
    [InlineData("1.284.350", 1284350)]
    [InlineData("0,125", 0.125)]
    [InlineData("7", 7)]
    public void ParseDecimal_UnambiguousFormats_ReturnSameNumberInBothConventions(string value, decimal expected)
    {
        ItemImportRules.ParseDecimal(value, decimalComma: true).ShouldBe(expected);
        ItemImportRules.ParseDecimal(value, decimalComma: false).ShouldBe(expected);
    }

    // Regression: Serbian Excel writes two thousand as "2.000"; it was read as 2, so prices of
    // 1,000 RSD and more came out a thousand times too small.
    [Theory]
    [InlineData("2.000", true, 2000)]
    [InlineData("1.400", true, 1400)]
    [InlineData("2,000", true, 2)]
    [InlineData("2,000", false, 2000)]
    [InlineData("2.000", false, 2)]
    public void ParseDecimal_SingleSeparatorWithThreeDigits_FollowsFileConvention(string value, bool decimalComma, decimal expected)
    {
        ItemImportRules.ParseDecimal(value, decimalComma).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12kg")]
    public void ParseDecimal_NotANumber_ReturnsNull(string value)
    {
        ItemImportRules.ParseDecimal(value).ShouldBeNull();
    }

    [Fact]
    public void ReadRow_ValidRow_ReturnsItemData()
    {
        // Act
        var result = Read("Kafa Etiopija 250 g", "KF-ETI-250", "kom", "900,5", "10", "12,5");

        // Assert
        result.Errors.ShouldBeEmpty();
        result.RowNumber.ShouldBe(7);
        result.Item.ShouldBe(new ImportedItem("Kafa Etiopija 250 g", "KF-ETI-250", null, Unit.Kom, null, null, 900.5m, null, 10, 12.5m));
    }

    [Fact]
    public void ReadRow_EmptyOptionalCells_UsesDefaultsAndZeros()
    {
        // Act
        var result = Read("Kafa", "K-1", "", "", "", "");

        // Assert
        result.Item.ShouldNotBeNull();
        result.Item.Unit.ShouldBe(Unit.Kom);
        result.Item.MinStock.ShouldBe(0);
        result.Item.InitialStock.ShouldBe(0);
        result.Item.PurchasePrice.ShouldBeNull();
    }

    [Fact]
    public void ReadRow_SeveralProblems_ReportsEachWithFieldAndParams()
    {
        // Act
        var result = Read("", "K-1", "kutija", "skupo", "-3", "1,2345");

        // Assert
        result.Item.ShouldBeNull();
        result.Errors.Select(e => (e.Code, e.Field)).ShouldBe(
        [
            ("validation.required", "name"),
            ("import.unit_unknown", "unit"),
            ("import.not_a_number", "purchasePrice"),
            ("validation.not_negative", "minStock"),
            ("validation.too_many_decimals", "initialStock"),
        ]);
        result.Errors[1].Params!["value"].ShouldBe("kutija");
    }
}
