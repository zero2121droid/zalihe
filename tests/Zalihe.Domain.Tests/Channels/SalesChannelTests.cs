using Shouldly;
using Zalihe.Domain.Channels;

namespace Zalihe.Domain.Tests.Channels;

public class SalesChannelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private static SalesChannel Create() =>
        new(Guid.NewGuid(), SalesChannelType.WooCommerce, "https://zrno.rs/", "enc-credentials", "enc-secret", Now);

    [Theory]
    [InlineData("https://zrno.rs", "https://zrno.rs")]
    [InlineData("  https://zrno.rs/  ", "https://zrno.rs")]
    [InlineData("zrno.rs", "https://zrno.rs")]
    [InlineData("https://zrno.rs/prodavnica/", "https://zrno.rs/prodavnica")]
    [InlineData("HTTPS://Zrno.RS", "https://zrno.rs")]
    [InlineData("http://localhost:8080", "http://localhost:8080")]
    [InlineData("http://127.0.0.1:8080/", "http://127.0.0.1:8080")]
    public void NormalizeBaseUrl_UsableAddress_ReturnsNormalized(string input, string expected)
    {
        SalesChannel.NormalizeBaseUrl(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://zrno.rs")]
    [InlineData("ftp://zrno.rs")]
    [InlineData("https://zrno.rs/?a=1")]
    [InlineData("nije adresa")]
    public void NormalizeBaseUrl_UnusableAddress_ReturnsNull(string input)
    {
        SalesChannel.NormalizeBaseUrl(input).ShouldBeNull();
    }

    [Fact]
    public void Constructor_NewChannel_IsConnectedWithNormalizedUrl()
    {
        // Act
        var channel = Create();

        // Assert
        channel.BaseUrl.ShouldBe("https://zrno.rs");
        channel.Status.ShouldBe(SalesChannelStatus.Connected);
        channel.LastErrorCode.ShouldBeNull();
    }

    [Fact]
    public void MarkFailed_ThenReplaceCredentials_GoesBackToConnected()
    {
        // Arrange
        var channel = Create();

        // Act & Assert
        channel.MarkFailed("channel.unauthorized", Now);
        channel.Status.ShouldBe(SalesChannelStatus.Error);
        channel.LastErrorCode.ShouldBe("channel.unauthorized");
        channel.FailingSince.ShouldBe(Now);

        channel.ReplaceCredentials("new-enc-credentials");
        channel.Status.ShouldBe(SalesChannelStatus.Connected);
        channel.LastErrorCode.ShouldBeNull();
        channel.FailingSince.ShouldBeNull();
        channel.EncryptedCredentials.ShouldBe("new-enc-credentials");
    }

    [Fact]
    public void RecordFailure_ShortOutage_StaysConnectedButKeepsReason()
    {
        // Arrange
        var channel = Create();

        // Act
        channel.RecordFailure("channel.unreachable", Now);
        channel.RecordFailure("channel.unreachable", Now + SalesChannel.ErrorAfter - TimeSpan.FromSeconds(1));

        // Assert
        channel.Status.ShouldBe(SalesChannelStatus.Connected);
        channel.LastErrorCode.ShouldBe("channel.unreachable");
        channel.FailingSince.ShouldBe(Now);
    }

    [Fact]
    public void RecordFailure_FailingForErrorAfter_ShowsErrorWithLatestReason()
    {
        // Arrange
        var channel = Create();
        channel.RecordFailure("channel.unreachable", Now);

        // Act
        channel.RecordFailure("channel.unauthorized", Now + SalesChannel.ErrorAfter);

        // Assert
        channel.Status.ShouldBe(SalesChannelStatus.Error);
        channel.LastErrorCode.ShouldBe("channel.unauthorized");
        channel.FailingSince.ShouldBe(Now);
    }

    [Fact]
    public void MarkConnected_AfterFailures_StartsCountingAgainOnNextFailure()
    {
        // Arrange
        var channel = Create();
        channel.RecordFailure("channel.unreachable", Now);
        channel.RecordFailure("channel.unreachable", Now + SalesChannel.ErrorAfter);

        // Act
        channel.MarkConnected();
        channel.RecordFailure("channel.unreachable", Now + TimeSpan.FromHours(2));

        // Assert
        channel.Status.ShouldBe(SalesChannelStatus.Connected);
        channel.FailingSince.ShouldBe(Now + TimeSpan.FromHours(2));
    }

    [Fact]
    public void MarkSynced_OlderRunFinishesLast_KeepsNewerTime()
    {
        // Arrange
        var channel = Create();
        channel.MarkSynced(Now.AddMinutes(15));

        // Act
        channel.MarkSynced(Now);

        // Assert
        channel.LastSyncedAt.ShouldBe(Now.AddMinutes(15));
    }

    [Fact]
    public void MarkSynced_NeverSynced_RemembersStart()
    {
        // Arrange
        var channel = Create();

        // Act
        channel.MarkSynced(Now);

        // Assert
        channel.LastSyncedAt.ShouldBe(Now);
    }
}
