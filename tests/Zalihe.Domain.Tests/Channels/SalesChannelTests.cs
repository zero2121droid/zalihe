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
        channel.MarkFailed("channel.unauthorized");
        channel.Status.ShouldBe(SalesChannelStatus.Error);
        channel.LastErrorCode.ShouldBe("channel.unauthorized");

        channel.ReplaceCredentials("new-enc-credentials");
        channel.Status.ShouldBe(SalesChannelStatus.Connected);
        channel.LastErrorCode.ShouldBeNull();
        channel.EncryptedCredentials.ShouldBe("new-enc-credentials");
    }
}
