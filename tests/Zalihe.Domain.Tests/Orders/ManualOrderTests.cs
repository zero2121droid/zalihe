using Shouldly;
using Zalihe.Domain.Channels;
using Zalihe.Domain.Orders;

namespace Zalihe.Domain.Tests.Orders;

public class ManualOrderTests
{
    private static readonly DateTimeOffset T1 = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddMinutes(5);
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Coffee = Guid.NewGuid();
    private static readonly Guid Shirt = Guid.NewGuid();

    private static ExternalOrder NewOrder(params ManualOrderLine[] lines) =>
        ExternalOrder.CreateManual(Tenant, 1, lines, null, null, T1).Order;

    [Fact]
    public void CreateManual_TwoItems_IsConfirmedAndTakesBoth()
    {
        // Act
        var (order, changes) = ExternalOrder.CreateManual(Tenant, 7, [new(Coffee, 2.5m), new(Shirt, 1)], " IG 154 ", " paket ", T1);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, -2.5m), new OrderStockChange(Shirt, -1)]);
        order.Source.ShouldBe(OrderSource.Manual);
        order.ChannelId.ShouldBeNull();
        order.TenantId.ShouldBe(Tenant);
        order.ExternalId.ShouldBe("R-0007");
        order.Reference.ShouldBe("IG 154");
        order.Note.ShouldBe("paket");
        order.Status.ShouldBe(ManualOrderStatus.Confirmed);
        order.TakesStock.ShouldBeTrue();
        order.PlacedAt.ShouldBe(T1);
    }

    [Fact]
    public void CreateManual_SameItemTwice_IsOneLine()
    {
        // Act
        var (order, changes) = ExternalOrder.CreateManual(Tenant, 1, [new(Coffee, 1), new(Coffee, 2)], null, null, T1);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, -3)]);
        order.Lines.ShouldHaveSingleItem().ItemId.ShouldBe(Coffee);
    }

    [Theory]
    [InlineData(1, "R-0001")]
    [InlineData(9999, "R-9999")]
    [InlineData(10000, "R-10000")]
    public void ManualNumber_PadsToFourDigits(int number, string expected)
    {
        ExternalOrder.ManualNumber(number).ShouldBe(expected);
    }

    [Fact]
    public void CreateManual_NoLines_Throws()
    {
        Should.Throw<ArgumentException>(() => ExternalOrder.CreateManual(Tenant, 1, [], null, null, T1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateManual_QuantityNotPositive_Throws(decimal quantity)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ExternalOrder.CreateManual(Tenant, 1, [new(Coffee, quantity)], null, null, T1));
    }

    [Fact]
    public void CreateManual_EmptyReferenceAndNote_AreNull()
    {
        // Act
        var (order, _) = ExternalOrder.CreateManual(Tenant, 1, [new(Coffee, 1)], "  ", "", T1);

        // Assert
        order.Reference.ShouldBeNull();
        order.Note.ShouldBeNull();
    }

    [Fact]
    public void SetManualStatus_Cancelled_ReturnsEverything()
    {
        // Arrange
        var order = NewOrder(new ManualOrderLine(Coffee, 2), new ManualOrderLine(Shirt, 1));

        // Act
        var changes = order.SetManualStatus(ManualOrderStatus.Cancelled, T2);

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, 2), new OrderStockChange(Shirt, 1)]);
        order.Status.ShouldBe(ManualOrderStatus.Cancelled);
        order.TakesStock.ShouldBeFalse();
        order.Lines.Select(l => l.AppliedQuantity).ShouldBe([0m, 0m]);
    }

    [Fact]
    public void SetManualStatus_ConfirmedAgainAfterCancel_TakesAgain()
    {
        // Arrange
        var order = NewOrder(new ManualOrderLine(Coffee, 2));
        order.SetManualStatus(ManualOrderStatus.Cancelled, T2);

        // Act
        var changes = order.SetManualStatus(ManualOrderStatus.Confirmed, T2.AddMinutes(1));

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, -2)]);
        order.TakesStock.ShouldBeTrue();
    }

    [Fact]
    public void SetManualStatus_SameStatusTwice_ChangesNothing()
    {
        // Arrange
        var order = NewOrder(new ManualOrderLine(Coffee, 2));
        order.SetManualStatus(ManualOrderStatus.Cancelled, T2);

        // Act
        var changes = order.SetManualStatus(ManualOrderStatus.Cancelled, T2.AddMinutes(1));

        // Assert
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void SetManualStatus_ClockBehindLastChange_IsStillApplied()
    {
        // Arrange
        var order = NewOrder(new ManualOrderLine(Coffee, 2));

        // Act
        var changes = order.SetManualStatus(ManualOrderStatus.Cancelled, T1.AddMinutes(-5));

        // Assert
        changes.ShouldBe([new OrderStockChange(Coffee, 2)]);
        order.LastModifiedAt.ShouldBe(T1);
    }

    [Fact]
    public void SetManualStatus_UnknownStatus_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NewOrder(new ManualOrderLine(Coffee, 1)).SetManualStatus("processing", T2));
    }

    [Fact]
    public void SetManualStatus_ShopOrder_Throws()
    {
        // Arrange
        var channel = new SalesChannel(Guid.NewGuid(), SalesChannelType.WooCommerce, "https://zrno.rs", "enc", "enc", T1);
        var order = new ExternalOrder(channel, "1041", T1, T1);
        order.Apply("processing", T1, [new("10", Coffee, 1)]);

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => order.SetManualStatus(ManualOrderStatus.Cancelled, T2));
    }

    [Fact]
    public void TakesStock_ShopStatusConfirmed_IsFalse()
    {
        // Arrange: "confirmed" means nothing to a shop order, so it doesn't take stock.
        var channel = new SalesChannel(Guid.NewGuid(), SalesChannelType.WooCommerce, "https://zrno.rs", "enc", "enc", T1);
        var order = new ExternalOrder(channel, "1041", T1, T1);

        // Act
        var changes = order.Apply(ManualOrderStatus.Confirmed, T1, [new("10", Coffee, 1)]);

        // Assert
        changes.ShouldBeEmpty();
        order.TakesStock.ShouldBeFalse();
        order.Source.ShouldBe(OrderSource.WooCommerce);
    }
}
