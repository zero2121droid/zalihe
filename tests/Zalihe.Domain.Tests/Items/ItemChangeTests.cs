using Shouldly;
using Zalihe.Domain.Items;

namespace Zalihe.Domain.Tests.Items;

public class ItemChangeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private static Item NewItem() =>
        new(Guid.NewGuid(), "Kafa", "KF-1", Unit.Kom, 10, Now, category: "Kafa", purchasePrice: 900);

    [Fact]
    public void Update_SomeFieldsChanged_ReturnsOnlyThoseWithInvariantValues()
    {
        // Arrange
        var item = NewItem();

        // Act
        var changes = item.Update("Kafa", "KF-1", Unit.Kg, 12.5m, category: null, purchasePrice: 950.5m);

        // Assert
        changes.Select(c => (c.Field, c.OldValue, c.NewValue)).ShouldBe(
        [
            ("unit", "kom", "kg"),
            ("category", "Kafa", null),
            ("purchasePrice", "900", "950.5"),
            ("minStock", "10", "12.5"),
        ]);
    }

    [Fact]
    public void Update_SameData_ReturnsNoChanges()
    {
        // Arrange
        var item = NewItem();

        // Act
        var changes = item.Update(" Kafa ", "KF-1", Unit.Kom, 10.0m, category: "Kafa", purchasePrice: 900.00m);

        // Assert
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void Updated_NoChanges_ReturnsNull()
    {
        ItemChange.Updated(NewItem(), [], UserId, Now).ShouldBeNull();
    }

    [Fact]
    public void Created_NewItem_RecordsKindUserAndTime()
    {
        // Arrange
        var item = NewItem();

        // Act
        var change = ItemChange.Created(item, UserId, Now);

        // Assert
        change.Kind.ShouldBe(ItemChangeKind.Created);
        change.ItemId.ShouldBe(item.Id);
        change.TenantId.ShouldBe(item.TenantId);
        change.UserId.ShouldBe(UserId);
        change.OccurredAt.ShouldBe(Now);
        change.Changes.ShouldBeEmpty();
    }

    [Fact]
    public void ActiveChanged_AfterDeactivateAndActivate_RecordsMatchingKind()
    {
        // Arrange
        var item = NewItem();

        // Act
        item.Deactivate();
        var deactivated = ItemChange.ActiveChanged(item, UserId, Now);
        item.Activate();
        var activated = ItemChange.ActiveChanged(item, UserId, Now);

        // Assert
        deactivated.Kind.ShouldBe(ItemChangeKind.Deactivated);
        activated.Kind.ShouldBe(ItemChangeKind.Activated);
    }
}
