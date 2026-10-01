using Shouldly;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;

namespace Zalihe.Domain.Tests.Stock;

public class StockMovementTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Item Item = new(Guid.NewGuid(), "Kafa", "KF-1", Unit.Kom, 10, Now);

    [Fact]
    public void Receipt_PositiveQuantity_AddsToStock()
    {
        // Act
        var movement = StockMovement.Receipt(Item, 24, Now, StockMovementSource.Manual, UserId, " Dobavljač ");

        // Assert
        movement.Type.ShouldBe(StockMovementType.Receipt);
        movement.Quantity.ShouldBe(24);
        movement.ItemId.ShouldBe(Item.Id);
        movement.TenantId.ShouldBe(Item.TenantId);
        movement.UserId.ShouldBe(UserId);
        movement.Note.ShouldBe("Dobavljač");
    }

    [Fact]
    public void Sale_PositiveQuantity_IsStoredAsNegative()
    {
        StockMovement.Sale(Item, 2, Now, StockMovementSource.Manual, UserId).Quantity.ShouldBe(-2);
    }

    [Fact]
    public void Return_PositiveQuantity_AddsToStock()
    {
        StockMovement.Return(Item, 1, Now, StockMovementSource.WooCommerce, null, externalRef: "1041").Quantity.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Receipt_ZeroOrNegative_Throws(decimal quantity)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StockMovement.Receipt(Item, quantity, Now, StockMovementSource.Manual, UserId));
    }

    [Fact]
    public void Sale_NegativeQuantity_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StockMovement.Sale(Item, -2, Now, StockMovementSource.Manual, UserId));
    }

    [Fact]
    public void Receipt_FourDecimals_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StockMovement.Receipt(Item, 1.2345m, Now, StockMovementSource.Manual, UserId));
    }

    [Theory]
    [InlineData(27, 30, -3)]
    [InlineData(12.5, 10, 2.5)]
    [InlineData(0, -2, 2)]
    public void AdjustmentToCount_CountDiffersFromStock_RecordsDifference(decimal counted, decimal current, decimal expected)
    {
        // Act
        var movement = StockMovement.AdjustmentToCount(Item, counted, current, Now, UserId, "Popis");

        // Assert
        movement.ShouldNotBeNull();
        movement.Type.ShouldBe(StockMovementType.Adjustment);
        movement.Quantity.ShouldBe(expected);
    }

    [Fact]
    public void AdjustmentToCount_CountEqualsStock_ReturnsNull()
    {
        StockMovement.AdjustmentToCount(Item, 30, 30, Now, UserId, "Popis").ShouldBeNull();
    }

    [Fact]
    public void AdjustmentToCount_NegativeCount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => StockMovement.AdjustmentToCount(Item, -1, 5, Now, UserId, "Popis"));
    }

    [Fact]
    public void Adjustment_ZeroDifference_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            StockMovement.Adjustment(Item, 0, Now, StockMovementSource.Manual, UserId));
    }
}
