using Shouldly;
using Zalihe.Application.Channels;

namespace Zalihe.Application.Tests.Channels;

public class ProductImportRulesTests
{
    private static readonly Dictionary<string, Guid> NothingLinked = [];

    private static ExternalProduct Product(string id, string? sku, string name = "Kafa", decimal? stock = 5) =>
        new(id, null, name, null, sku, null, 1250m, null, stock);

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

    private static ExternalProduct ShopProduct(string name = "Kafa Etiopija", string? barcode = null, string? category = null, decimal? price = null, decimal? cost = null) =>
        new("10", null, name, null, "KF-ETI-250", category, price, cost, 5, null, barcode);

    private static ExistingItem Linked(string name = "Kafa Etiopija", string? barcode = null, string? category = null, decimal? salePrice = null, decimal? purchasePrice = null) =>
        new(Guid.NewGuid(), name, "KF-ETI-250", barcode, category, salePrice, purchasePrice);

    [Fact]
    public void Differences_SameData_IsEmpty()
    {
        ProductImportRules.Differences(ShopProduct(barcode: "8600123456789", price: 1250), Linked(barcode: "8600123456789", salePrice: 1250))
            .ShouldBeEmpty();
    }

    [Fact]
    public void Differences_ItemWithoutBarcode_OffersShopsBarcodeAsFillingEmpty()
    {
        // Act
        var update = ProductImportRules.Differences(ShopProduct(barcode: " 8600123456789 "), Linked()).ShouldHaveSingleItem();

        // Assert
        update.ShouldBe(new FieldUpdateDto("barcode", null, "8600123456789", FillsEmpty: true, LargeChange: false));
    }

    [Fact]
    public void Differences_OtherPrice_OffersItButNotAsFillingEmpty()
    {
        // Act
        var update = ProductImportRules.Differences(ShopProduct(price: 1390), Linked(salePrice: 1250)).ShouldHaveSingleItem();

        // Assert
        update.ShouldBe(new FieldUpdateDto("salePrice", "1250", "1390", FillsEmpty: false, LargeChange: false));
    }

    [Fact]
    public void Differences_PriceMoreThanTwiceApart_IsALargeChange()
    {
        // Act
        var update = ProductImportRules.Differences(ShopProduct(price: 500), Linked(salePrice: 4000)).ShouldHaveSingleItem();

        // Assert
        update.LargeChange.ShouldBeTrue();
    }

    [Fact]
    public void Differences_ShopHasNoValue_NeverOffersToClearTheItems()
    {
        ProductImportRules.Differences(ShopProduct(), Linked(barcode: "8600123456789", category: "Kafa", salePrice: 1250, purchasePrice: 600))
            .ShouldBeEmpty();
    }

    [Fact]
    public void Differences_RenamedAndRecategorized_OffersBoth()
    {
        // Act
        var updates = ProductImportRules.Differences(ShopProduct(name: "Kafa Etiopija 250 g", category: "Kafa · zrno"), Linked(category: "Kafa"));

        // Assert
        updates.Select(u => (u.Field, u.Current, u.Shop)).ShouldBe([
            ("name", "Kafa Etiopija", "Kafa Etiopija 250 g"),
            ("category", "Kafa", "Kafa · zrno"),
        ]);
    }
}
