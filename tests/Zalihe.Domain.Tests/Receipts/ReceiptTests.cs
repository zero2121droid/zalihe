using Shouldly;
using Zalihe.Domain.Items;
using Zalihe.Domain.Receipts;

namespace Zalihe.Domain.Tests.Receipts;

public class ReceiptTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 11, 2);
    private static readonly Guid Tenant = Guid.NewGuid();

    private static Item NewItem(Guid? tenantId = null, decimal? purchasePrice = null) =>
        new(tenantId ?? Tenant, "Kafa Etiopija 250 g", "KF-ETI-250", Unit.Kom, 10, Now, purchasePrice: purchasePrice);

    private static Receipt NewReceipt(string? documentNumber = "F-154") => new(Tenant, " Kafa Import ", documentNumber, Date, null, Now);

    [Fact]
    public void Constructor_TrimsAndStartsAsDraft()
    {
        // Act
        var receipt = new Receipt(Tenant, " Kafa Import ", "  ", Date, null, Now);

        // Assert
        receipt.SupplierName.ShouldBe("Kafa Import");
        receipt.DocumentNumber.ShouldBeNull();
        receipt.DocumentKey.ShouldBeNull();
        receipt.Status.ShouldBe(ReceiptStatus.Draft);
        receipt.PostedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_NoSupplier_Throws(string supplier)
    {
        Should.Throw<ArgumentException>(() => new Receipt(Tenant, supplier, null, Date, null, Now));
    }

    [Fact]
    public void Post_WithLines_IsPostedWithTime()
    {
        // Arrange
        var receipt = NewReceipt();
        var line = receipt.AddLine(NewItem(), 24, 1250.5m);
        var second = receipt.AddLine(NewItem(), 1, null);

        // Act
        receipt.Post(Now);

        // Assert
        receipt.Status.ShouldBe(ReceiptStatus.Posted);
        receipt.PostedAt.ShouldBe(Now);
        line.ReceiptId.ShouldBe(receipt.Id);
        line.TenantId.ShouldBe(Tenant);
        line.Quantity.ShouldBe(24);
        line.UnitPrice.ShouldBe(1250.5m);
        line.Position.ShouldBe(0);
        second.Position.ShouldBe(1);
    }

    [Fact]
    public void Post_NoLines_Throws()
    {
        Should.Throw<InvalidOperationException>(() => NewReceipt().Post(Now));
    }

    [Fact]
    public void AddLine_AfterPosting_Throws()
    {
        // Arrange
        var receipt = NewReceipt();
        receipt.AddLine(NewItem(), 1, null);
        receipt.Post(Now);

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => receipt.AddLine(NewItem(), 1, null));
        Should.Throw<InvalidOperationException>(() => receipt.Post(Now));
    }

    [Fact]
    public void AddLine_OtherCompanysItem_Throws()
    {
        Should.Throw<InvalidOperationException>(() => NewReceipt().AddLine(NewItem(Guid.NewGuid()), 1, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.0001)]
    public void AddLine_BadQuantity_Throws(decimal quantity)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NewReceipt().AddLine(NewItem(), quantity, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.001)]
    public void AddLine_BadPrice_Throws(decimal price)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NewReceipt().AddLine(NewItem(), 1, price));
    }

    [Theory]
    [InlineData("Kafa Import", "F-154", "KAFA  import ", " f-154")]
    [InlineData("Kafa Import d.o.o.", "F 154", "kafa import D.O.O.", "F   154")]
    public void DocumentKey_SpacesAndCaseDiffer_IsTheSame(string supplierA, string numberA, string supplierB, string numberB)
    {
        Receipt.KeyFor(supplierA, numberA).ShouldBe(Receipt.KeyFor(supplierB, numberB));
    }

    [Fact]
    public void DocumentKey_NoNumber_IsNull()
    {
        Receipt.KeyFor("Kafa Import", " ").ShouldBeNull();
    }

    [Fact]
    public void ChangePurchasePrice_NewPrice_ReturnsOnlyThatChange()
    {
        // Arrange
        var item = NewItem(purchasePrice: 1000);

        // Act
        var changes = item.ChangePurchasePrice(1100);

        // Assert
        changes.ShouldHaveSingleItem().Field.ShouldBe("purchasePrice");
        item.PurchasePrice.ShouldBe(1100);
        item.ChangePurchasePrice(1100).ShouldBeEmpty();
    }
}
