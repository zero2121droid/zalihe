using Shouldly;
using Zalihe.Domain.Items;

namespace Zalihe.Domain.Tests.Items;

public class ItemTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static Item Create(
        string name = "Kafa Etiopija 250 g",
        string sku = "KF-ETI-250",
        decimal minStock = 10,
        string? barcode = null,
        string? category = null,
        decimal? purchasePrice = null) =>
        new(TenantId, name, sku, Unit.Kom, minStock, Now, barcode, category, purchasePrice: purchasePrice);

    [Fact]
    public void Constructor_ValidData_CreatesActiveItem()
    {
        // Act
        var item = Create();

        // Assert
        item.Id.ShouldNotBe(Guid.Empty);
        item.TenantId.ShouldBe(TenantId);
        item.IsActive.ShouldBeTrue();
        item.Unit.ShouldBe(Unit.Kom);
        item.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Constructor_TextWithSurroundingWhitespace_TrimsIt()
    {
        // Act
        var item = Create(name: "  Kafa  ", sku: " KF-1 ", category: " Kafa · zrno ");

        // Assert
        item.Name.ShouldBe("Kafa");
        item.Sku.ShouldBe("KF-1");
        item.Category.ShouldBe("Kafa · zrno");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOptionalText_StoresNull(string? value)
    {
        // Act
        var item = Create(barcode: value, category: value);

        // Assert
        item.Barcode.ShouldBeNull();
        item.Category.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "KF-1")]
    [InlineData("Kafa", " ")]
    public void Constructor_MissingNameOrSku_Throws(string name, string sku)
    {
        Should.Throw<ArgumentException>(() => Create(name: name, sku: sku));
    }

    [Fact]
    public void Constructor_NegativeMinStock_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Create(minStock: -1));
    }

    [Fact]
    public void Constructor_MinStockWithFourDecimals_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Create(minStock: 1.2345m));
    }

    [Fact]
    public void Constructor_DecimalMinStock_IsAllowed()
    {
        Create(minStock: 12.5m).MinStock.ShouldBe(12.5m);
    }

    [Fact]
    public void Constructor_PriceWithThreeDecimals_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Create(purchasePrice: 9.999m));
    }

    [Fact]
    public void Constructor_NoTenant_Throws()
    {
        Should.Throw<ArgumentException>(() => new Item(Guid.Empty, "Kafa", "KF-1", Unit.Kom, 0, Now));
    }

    [Theory]
    [InlineData(12.5, 3, true)]
    [InlineData(12.125, 3, true)]
    [InlineData(12.1255, 3, false)]
    [InlineData(99.99, 2, true)]
    [InlineData(99.999, 2, false)]
    public void HasAtMostDecimals_Value_ReturnsExpected(double value, int decimals, bool expected)
    {
        Item.HasAtMostDecimals((decimal)value, decimals).ShouldBe(expected);
    }
}
