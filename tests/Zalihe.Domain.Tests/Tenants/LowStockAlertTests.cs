using Shouldly;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Tests.Tenants;

public class LowStockAlertTests
{
    private static readonly TimeZoneInfo Belgrade = TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");

    /// <summary>A moment by the Belgrade clock (UTC+2 in October).</summary>
    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(2));

    private static readonly Guid Coffee = Guid.NewGuid();
    private static readonly Guid Tea = Guid.NewGuid();
    private static readonly Guid Filter = Guid.NewGuid();

    private static LowStockAlert Alert() => new(Guid.NewGuid());

    [Fact]
    public void Check_FirstLowItems_SendsAllOfThemAsNew()
    {
        // Arrange
        var alert = Alert();

        // Act
        var news = alert.Check([Coffee, Tea], At(19, 9), Belgrade);

        // Assert
        news.ShouldBe([Coffee, Tea]);
    }

    [Fact]
    public void Check_NothingLow_SendsNothing()
    {
        Alert().Check([], At(19, 9), Belgrade).ShouldBeEmpty();
    }

    [Fact]
    public void Check_SameItemsStillLowNextDay_SendsNothing()
    {
        // Arrange
        var alert = Alert();
        alert.MarkSent([Coffee, Tea], At(19, 9));

        // Act
        var news = alert.Check([Coffee, Tea], At(20, 9), Belgrade);

        // Assert
        news.ShouldBeEmpty();
    }

    [Fact]
    public void Check_AnotherItemFallsTheSameDay_WaitsForTomorrow()
    {
        // Arrange
        var alert = Alert();
        alert.MarkSent([Coffee], At(19, 9));

        // Act
        var sameDay = alert.Check([Coffee, Tea], At(19, 20, 59), Belgrade);
        var nextDay = alert.Check([Coffee, Tea], At(20, 7), Belgrade);

        // Assert
        sameDay.ShouldBeEmpty();
        nextDay.ShouldBe([Tea]);
    }

    [Theory]
    [InlineData(6, 59)]
    [InlineData(21, 0)]
    [InlineData(23, 30)]
    public void Check_AtNight_SendsNothing(int hour, int minute)
    {
        Alert().Check([Coffee], At(19, hour, minute), Belgrade).ShouldBeEmpty();
    }

    [Fact]
    public void Check_DayIsCountedByTheCompanysClockNotUtc()
    {
        // Arrange: an email at 23:30 UTC on the 19th went out at 1:30 on the 20th in Belgrade.
        var alert = Alert();
        alert.MarkSent([Coffee], new DateTimeOffset(2026, 10, 19, 23, 30, 0, TimeSpan.Zero));

        // Act: 9:00 on the 20th in Belgrade is the same Belgrade day as that email.
        var news = alert.Check([Coffee, Tea], At(20, 9), Belgrade);

        // Assert
        news.ShouldBeEmpty();
    }

    [Fact]
    public void Check_ItemRecoversAndFallsAgain_IsNewAgain()
    {
        // Arrange
        var alert = Alert();
        alert.MarkSent([Coffee, Tea], At(19, 9));
        alert.Check([Tea], At(20, 9), Belgrade);          // coffee was received: forgotten

        // Act
        var news = alert.Check([Coffee, Tea], At(21, 9), Belgrade);

        // Assert
        news.ShouldBe([Coffee]);
    }

    [Fact]
    public void Check_ItemRecoveredWhileNoEmailWasDue_IsForgottenAnyway()
    {
        // Arrange
        var alert = Alert();
        alert.MarkSent([Coffee, Filter], At(19, 9));

        // Act
        alert.Check([Coffee], At(19, 23), Belgrade);

        // Assert
        alert.ReportedItemIds.ShouldBe([Coffee]);
    }

    [Fact]
    public void MarkSent_RemembersTheListAndTime()
    {
        var alert = Alert();

        alert.MarkSent([Coffee, Tea, Coffee], At(19, 9));

        alert.ReportedItemIds.ShouldBe([Coffee, Tea]);
        alert.LastSentAt.ShouldBe(At(19, 9));
    }
}
