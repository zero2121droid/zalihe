using Shouldly;
using Zalihe.Domain.Channels;

namespace Zalihe.Domain.Tests.Channels;

public class WebhookEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly SalesChannel Channel =
        new(Guid.NewGuid(), SalesChannelType.WooCommerce, "https://zrno.rs", "enc-credentials", "enc-secret", Now);

    private static WebhookEvent NewEvent() => new(Channel, "order.updated", """{"ExternalId":"1041"}""", Now);

    [Fact]
    public void Constructor_TakesCompanyAndShopFromChannel()
    {
        // Act
        var webhookEvent = NewEvent();

        // Assert
        webhookEvent.TenantId.ShouldBe(Channel.TenantId);
        webhookEvent.ChannelId.ShouldBe(Channel.Id);
        webhookEvent.ReceivedAt.ShouldBe(Now);
        webhookEvent.IsProcessed.ShouldBeFalse();
    }

    [Fact]
    public void MarkProcessed_AfterFailure_ClearsError()
    {
        // Arrange
        var webhookEvent = NewEvent();
        webhookEvent.MarkFailed("DbUpdateException: timeout");

        // Act
        webhookEvent.MarkProcessed(Now.AddMinutes(1));

        // Assert
        webhookEvent.IsProcessed.ShouldBeTrue();
        webhookEvent.Error.ShouldBeNull();
    }

    [Fact]
    public void MarkFailed_LongError_IsCutToColumnLength()
    {
        // Arrange
        var webhookEvent = NewEvent();

        // Act
        webhookEvent.MarkFailed(new string('x', WebhookEvent.ErrorMaxLength + 50));

        // Assert
        webhookEvent.Error!.Length.ShouldBe(WebhookEvent.ErrorMaxLength);
        webhookEvent.IsProcessed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_MissingTopic_Throws(string topic)
    {
        Should.Throw<ArgumentException>(() => new WebhookEvent(Channel, topic, "{}", Now));
    }
}
