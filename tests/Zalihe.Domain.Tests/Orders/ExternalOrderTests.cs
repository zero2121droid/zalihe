using Shouldly;
using Zalihe.Domain.Channels;
using Zalihe.Domain.Orders;

namespace Zalihe.Domain.Tests.Orders;

public class ExternalOrderTests
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddMinutes(5);
    private static readonly DateTimeOffset T3 = T1.AddMinutes(10);
    private static readonly Guid Coffee = Guid.NewGuid();
    private static readonly Guid Shirt = Guid.NewGuid();

    private static ExternalOrder NewOrder()
    {
        var channel = new SalesChannel(Guid.NewGuid(), SalesChannelType.WooCommerce, "https://zrno.rs", "enc", "enc", T1);
        return new ExternalOrder(channel, " 1041 ", T1);
    }

    private static OrderLineSnapshot CoffeeLine(decimal quantity) => new("10", Coffee, quantity);

    private static OrderLineSnapshot ShirtLine(decimal quantity) => new("51", Shirt, quantity);

    [Fact]
    public void Apply_NewActiveOrder_TakesOrderedQuantities()
    {
        // Arrange
        var order = NewOrder();

        // Act
        var changes = order.Apply("processing", T1, [CoffeeLine(2), ShirtLine(1)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, -2), new OrderStockChange(Shirt, -1)]);
        order.ExternalId.ShouldBe("1041");
        order.Status.ShouldBe("processing");
        order.LastModifiedAt.ShouldBe(T1);
        order.Lines.Select(l => l.AppliedQuantity).ShouldBe([2m, 1m]);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("failed")]
    [InlineData("checkout-draft")]
    public void Apply_NewOrderNotYetActive_TakesNothing(string status)
    {
        // Arrange
        var order = NewOrder();

        // Act
        var changes = order.Apply(status, T1, [CoffeeLine(2)]);

        // Assert
        changes.ShouldBeEmpty();
        order.Lines.ShouldHaveSingleItem().OrderedQuantity.ShouldBe(2);
    }

    [Fact]
    public void Apply_DuplicatedEvent_ChangesNothingTheSecondTime()
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [CoffeeLine(2)]);

        // Act
        var changes = order.Apply("processing", T1, [CoffeeLine(2)]);

        // Assert
        changes.ShouldBeEmpty();
        order.Lines.ShouldHaveSingleItem().AppliedQuantity.ShouldBe(2);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("refunded")]
    [InlineData("failed")]
    public void Apply_ActiveOrderCancelled_ReturnsEverything(string status)
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [CoffeeLine(2), ShirtLine(1)]);

        // Act
        var changes = order.Apply(status, T2, [CoffeeLine(2), ShirtLine(1)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, 2), new OrderStockChange(Shirt, 1)]);
        order.Lines.ShouldAllBe(l => l.AppliedQuantity == 0);
    }

    [Fact]
    public void Apply_ReactivatedAfterCancel_TakesAgain()
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [CoffeeLine(2)]);
        order.Apply("cancelled", T2, [CoffeeLine(2)]);

        // Act
        var changes = order.Apply("processing", T3, [CoffeeLine(2)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, -2)]);
    }

    [Fact]
    public void Apply_CompletedAfterProcessing_ChangesNothing()
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [CoffeeLine(2)]);

        // Act
        var changes = order.Apply("completed", T2, [CoffeeLine(2)]);

        // Assert
        changes.ShouldBeEmpty();
        order.Status.ShouldBe("completed");
    }

    [Theory]
    [InlineData(5, -3)]
    [InlineData(1, 1)]
    public void Apply_QuantityChanged_AppliesOnlyTheDifference(decimal newQuantity, decimal expected)
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [CoffeeLine(2)]);

        // Act
        var changes = order.Apply("processing", T2, [CoffeeLine(newQuantity)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, expected)]);
        order.Lines.ShouldHaveSingleItem().AppliedQuantity.ShouldBe(newQuantity);
    }

    [Fact]
    public void Apply_LineRemoved_ReturnsItsStock()
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [CoffeeLine(2), ShirtLine(1)]);

        // Act
        var changes = order.Apply("processing", T2, [CoffeeLine(2)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Shirt, 1)]);
        var removed = order.Lines.Single(l => l.ItemId == Shirt);
        removed.OrderedQuantity.ShouldBe(0);
        removed.AppliedQuantity.ShouldBe(0);
    }

    [Fact]
    public void Apply_RemovedLineAddedBack_TakesAgain()
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [CoffeeLine(2), ShirtLine(1)]);
        order.Apply("processing", T2, [CoffeeLine(2)]);

        // Act
        var changes = order.Apply("processing", T3, [CoffeeLine(2), ShirtLine(3)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Shirt, -3)]);
        order.Lines.Count.ShouldBe(2);
    }

    [Fact]
    public void Apply_OlderEventAfterNewer_IsIgnored()
    {
        // Arrange: the cancellation (T2) arrives before the creation (T1)
        var order = NewOrder();
        order.Apply("cancelled", T2, [CoffeeLine(2)]);

        // Act
        var changes = order.Apply("processing", T1, [CoffeeLine(2)]);

        // Assert
        changes.ShouldBeEmpty();
        order.Status.ShouldBe("cancelled");
        order.LastModifiedAt.ShouldBe(T2);
        order.Lines.ShouldHaveSingleItem().AppliedQuantity.ShouldBe(0);
    }

    [Fact]
    public void Apply_EventsOutOfOrder_EndInTheSameStateAsInOrder()
    {
        // Arrange: created (T1), quantity changed (T2), cancelled (T3)
        var inOrder = NewOrder();
        var outOfOrder = NewOrder();
        var created = (Status: "processing", At: T1, Lines: new[] { CoffeeLine(2) });
        var changed = (Status: "processing", At: T2, Lines: new[] { CoffeeLine(5) });
        var cancelled = (Status: "cancelled", At: T3, Lines: new[] { CoffeeLine(5) });

        // Act
        var inOrderTotal = new[] { created, changed, cancelled }
            .SelectMany(e => inOrder.Apply(e.Status, e.At, e.Lines)).Sum(c => c.Quantity);
        var outOfOrderTotal = new[] { changed, cancelled, created }
            .SelectMany(e => outOfOrder.Apply(e.Status, e.At, e.Lines)).Sum(c => c.Quantity);

        // Assert
        inOrderTotal.ShouldBe(0);
        outOfOrderTotal.ShouldBe(0);
        outOfOrder.Status.ShouldBe("cancelled");
        outOfOrder.Lines.ShouldHaveSingleItem().AppliedQuantity.ShouldBe(0);
    }

    [Fact]
    public void Apply_SameProductOnTwoLines_TakesTheirSum()
    {
        // Arrange
        var order = NewOrder();

        // Act
        var changes = order.Apply("processing", T1, [CoffeeLine(2), CoffeeLine(1)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, -3)]);
        order.Lines.ShouldHaveSingleItem().OrderedQuantity.ShouldBe(3);
    }

    [Fact]
    public void Apply_UnlinkedProduct_IsKeptButTakesNothing()
    {
        // Arrange
        var order = NewOrder();

        // Act
        var changes = order.Apply("processing", T1, [new OrderLineSnapshot("99", null, 4), CoffeeLine(1)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, -1)]);
        var unlinked = order.Lines.Single(l => l.ExternalProductId == "99");
        unlinked.IsLinked.ShouldBeFalse();
        unlinked.OrderedQuantity.ShouldBe(4);
        unlinked.AppliedQuantity.ShouldBe(0);
    }

    [Fact]
    public void Apply_ProductLinkedLater_TakesItsQuantity()
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [new OrderLineSnapshot("99", null, 4)]);

        // Act: the same event again, now that the product is linked to an item
        var changes = order.Apply("processing", T1, [new OrderLineSnapshot("99", Coffee, 4)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, -4)]);
        order.Lines.ShouldHaveSingleItem().ItemId.ShouldBe(Coffee);
    }

    [Fact]
    public void Apply_ProductRelinkedAfterStockWasTaken_ReturnsToTheOriginalItem()
    {
        // Arrange
        var order = NewOrder();
        order.Apply("processing", T1, [CoffeeLine(2)]);

        // Act: the product now points to another item, and the order is cancelled
        var changes = order.Apply("cancelled", T2, [new OrderLineSnapshot("10", Shirt, 2)]);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, 2)]);
    }

    [Fact]
    public void Apply_NegativeQuantity_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NewOrder().Apply("processing", T1, [CoffeeLine(-1)]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Apply_MissingStatus_Throws(string status)
    {
        Should.Throw<ArgumentException>(() => NewOrder().Apply(status, T1, [CoffeeLine(1)]));
    }

    [Fact]
    public void Constructor_TakesCompanyAndShopFromChannel()
    {
        // Arrange
        var channel = new SalesChannel(Guid.NewGuid(), SalesChannelType.WooCommerce, "https://zrno.rs", "enc", "enc", T1);

        // Act
        var order = new ExternalOrder(channel, "1041", T1);
        order.Apply("processing", T1, [CoffeeLine(1)]);

        // Assert
        order.TenantId.ShouldBe(channel.TenantId);
        order.ChannelId.ShouldBe(channel.Id);
        order.Lines.ShouldHaveSingleItem().TenantId.ShouldBe(channel.TenantId);
        order.Lines[0].ExternalOrderId.ShouldBe(order.Id);
    }
}
