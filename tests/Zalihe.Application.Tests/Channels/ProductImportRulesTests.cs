using Shouldly;
using Zalihe.Application.Channels;

namespace Zalihe.Application.Tests.Channels;

public class ProductImportRulesTests
{
    private static readonly Dictionary<string, Guid> NothingLinked = [];

    private static ExternalProduct Product(string id, string? sku, string name = "Kafa", decimal? stock = 5) =>
        new(id, null, name, null, sku, null, 1250m, stock);

    [Fact]
    public void Classify_SkuOfExistingItem_LinksToThatItem()
    {
        // Arrange
        var item = new ExistingItem(Guid.NewGuid(), "Kafa Etiopija", "KF-ETI-250");

        // Act
        var row = ProductImportRules.Classify([Product("10", " KF-ETI-250 ")], [item], NothingLinked).ShouldHaveSingleItem();

        // Assert
        row.Kind.ShouldBe(ProductImportKind.Match);
        row.Item.ShouldBe(item);
    }

    [Fact]
    public void Classify_UnknownSku_IsNew()
    {
        ProductImportRules.Classify([Product("10", "WOO-ONLY-1")], [], NothingLinked)
            .ShouldHaveSingleItem().Kind.ShouldBe(ProductImportKind.New);
    }

    [Fact]
    public void Classify_SkuDiffersOnlyInCase_IsNew()
    {
        // SKUs are unique per company with exact spelling, so "kf-eti-250" is a different item.
        var item = new ExistingItem(Guid.NewGuid(), "Kafa", "KF-ETI-250");

        ProductImportRules.Classify([Product("10", "kf-eti-250")], [item], NothingLinked)
            .ShouldHaveSingleItem().Kind.ShouldBe(ProductImportKind.New);
    }

    [Fact]
    public void Classify_AlreadyLinkedProduct_IsLinkedEvenIfSkuChanged()
    {
        // Arrange
        var item = new ExistingItem(Guid.NewGuid(), "Kafa", "KF-ETI-250");
        var linked = new Dictionary<string, Guid> { ["10"] = item.Id };

        // Act
        var row = ProductImportRules.Classify([Product("10", "NOVA-SIFRA")], [item], linked).ShouldHaveSingleItem();

        // Assert
        row.Kind.ShouldBe(ProductImportKind.Linked);
        row.Item.ShouldBe(item);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_NoSku_IsSkipped(string? sku)
    {
        var row = ProductImportRules.Classify([Product("10", sku)], [], NothingLinked).ShouldHaveSingleItem();

        row.Kind.ShouldBe(ProductImportKind.Skipped);
        row.Error!.Code.ShouldBe(ProductImportRules.NoSku);
    }

    [Fact]
    public void Classify_SkuTooLong_IsSkippedWithMax()
    {
        var row = ProductImportRules.Classify([Product("10", new string('A', 65))], [], NothingLinked).ShouldHaveSingleItem();

        row.Error!.Code.ShouldBe(ProductImportRules.SkuTooLong);
        row.Error.Params!["max"].ShouldBe(64);
    }

    [Fact]
    public void Classify_SameSkuOnTwoProducts_SkipsBoth()
    {
        // Arrange: e.g. variations that inherit the variable product's SKU
        var item = new ExistingItem(Guid.NewGuid(), "Majica", "MAJ-BASIC");

        // Act
        var rows = ProductImportRules.Classify([Product("51", "MAJ-BASIC"), Product("52", "MAJ-BASIC")], [item], NothingLinked);

        // Assert
        rows.ShouldAllBe(r => r.Kind == ProductImportKind.Skipped && r.Error!.Code == ProductImportRules.SkuDuplicate);
    }

    [Fact]
    public void Classify_ItemAlreadyLinkedToAnotherProduct_IsSkipped()
    {
        // Arrange: the item was linked to product 10, and a new product 11 got its SKU
        var item = new ExistingItem(Guid.NewGuid(), "Kafa", "KF-ETI-250");
        var linked = new Dictionary<string, Guid> { ["10"] = item.Id };

        // Act
        var rows = ProductImportRules.Classify([Product("10", "STARA"), Product("11", "KF-ETI-250")], [item], linked);

        // Assert
        rows[0].Kind.ShouldBe(ProductImportKind.Linked);
        rows[1].Kind.ShouldBe(ProductImportKind.Skipped);
        rows[1].Error!.Code.ShouldBe(ProductImportRules.ItemLinkedElsewhere);
    }

    [Theory]
    [InlineData("1250.555", "1250.56")]
    [InlineData("-1", null)]
    [InlineData(null, null)]
    public void Price_ShopPrice_FitsItemPrice(string? value, string? expected)
    {
        ProductImportRules.Price(value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(24, 24)]
    [InlineData(-2, -2)]
    public void OpeningStock_ShopStock_IsUsedAsIs(int? value, int expected)
    {
        ProductImportRules.OpeningStock(value).ShouldBe(expected);
    }

    [Fact]
    public void Fit_LongName_IsCutToMaxLength()
    {
        ProductImportRules.Fit(" " + new string('a', 210) + " ", 200)!.Length.ShouldBe(200);
    }
}
