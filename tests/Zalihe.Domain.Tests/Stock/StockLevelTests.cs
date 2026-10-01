using Shouldly;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;

namespace Zalihe.Domain.Tests.Stock;

public class StockLevelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static Item NewItem() => new(Guid.NewGuid(), "Kafa", "KF-1", Unit.Kom, 10, Now);

    [Fact]
    public void Constructor_NewItem_StartsAtZero()
    {
        // Arrange
        var item = NewItem();

        // Act
        var level = new StockLevel(item, Now);

        // Assert
        level.Quantity.ShouldBe(0);
        level.ItemId.ShouldBe(item.Id);
        level.TenantId.ShouldBe(item.TenantId);
    }

    [Fact]
    public void Apply_SeveralMovements_EqualsTheirSum()
    {
        // Arrange
        var item = NewItem();
        var level = new StockLevel(item, Now);
        var movements = new[]
        {
            StockMovement.Receipt(item, 24, Now, StockMovementSource.Manual, null),
            StockMovement.Sale(item, 2.5m, Now.AddHours(1), StockMovementSource.Manual, null),
            StockMovement.Return(item, 1, Now.AddHours(2), StockMovementSource.WooCommerce, null),
            StockMovement.AdjustmentToCount(item, 20, 22.5m, Now.AddHours(3), null, "Popis")!,
        };

        // Act
        foreach (var movement in movements) level.Apply(movement);

        // Assert
        level.Quantity.ShouldBe(movements.Sum(m => m.Quantity));
        level.Quantity.ShouldBe(20);
        level.UpdatedAt.ShouldBe(Now.AddHours(3));
    }

    [Fact]
    public void Apply_SaleBeyondStock_GoesNegative()
    {
        // Arrange
        var item = NewItem();
        var level = new StockLevel(item, Now);

        // Act
        level.Apply(StockMovement.Sale(item, 2, Now, StockMovementSource.WooCommerce, null));

        // Assert
        level.Quantity.ShouldBe(-2);
    }

    [Fact]
    public void Apply_MovementOfAnotherItem_Throws()
    {
        // Arrange
        var level = new StockLevel(NewItem(), Now);
        var other = StockMovement.Receipt(NewItem(), 1, Now, StockMovementSource.Manual, null);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => level.Apply(other));
    }

    [Theory]
    [InlineData(31, 10, StockStatus.InStock)]
    [InlineData(10, 10, StockStatus.Low)]
    [InlineData(4, 10, StockStatus.Low)]
    [InlineData(0, 10, StockStatus.OutOfStock)]
    [InlineData(-2, 10, StockStatus.OutOfStock)]
    [InlineData(0, 0, StockStatus.OutOfStock)]
    [InlineData(0.5, 0, StockStatus.InStock)]
    public void StockStatusRules_QuantityAndMinimum_GiveStatus(decimal quantity, decimal minStock, StockStatus expected)
    {
        StockStatusRules.For(quantity, minStock).ShouldBe(expected);
    }
}
