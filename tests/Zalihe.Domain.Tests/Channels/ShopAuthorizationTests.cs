using Shouldly;
using Zalihe.Domain.Channels;

namespace Zalihe.Domain.Tests.Channels;

public class ShopAuthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static ShopAuthorization Start(string token = "tajni-token") =>
        new(Guid.NewGuid(), SalesChannelType.WooCommerce, "zrno.rs/", token, Now);

    [Fact]
    public void Constructor_Address_IsNormalizedAndOnlyTheTokensHashIsKept()
    {
        // Act
        var authorization = Start("tajni-token");

        // Assert
        authorization.BaseUrl.ShouldBe("https://zrno.rs");
        authorization.TokenHash.ShouldBe(ShopAuthorization.HashToken("tajni-token"));
        authorization.TokenHash.ShouldNotContain("tajni-token");
        authorization.ExpiresAt.ShouldBe(Now + ShopAuthorization.ValidFor);
        authorization.HasKeys.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_InvalidAddress_Throws()
    {
        Should.Throw<ArgumentException>(() => new ShopAuthorization(Guid.NewGuid(), SalesChannelType.WooCommerce, "http://zrno.rs", "t", Now));
    }

    [Fact]
    public void ReceiveKeys_InTime_KeepsKeysAndAcceptsNoMore()
    {
        // Arrange
        var authorization = Start();

        // Act
        authorization.ReceiveKeys("sifrovano", Now.AddMinutes(5));

        // Assert
        authorization.HasKeys.ShouldBeTrue();
        authorization.EncryptedCredentials.ShouldBe("sifrovano");
        authorization.KeysReceivedAt.ShouldBe(Now.AddMinutes(5));
        authorization.CanReceiveKeysAt(Now.AddMinutes(6)).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => authorization.ReceiveKeys("druge", Now.AddMinutes(6)));
        authorization.EncryptedCredentials.ShouldBe("sifrovano");
    }

    [Fact]
    public void ReceiveKeys_AfterExpiry_Throws()
    {
        // Arrange
        var authorization = Start();

        // Act & Assert
        authorization.CanReceiveKeysAt(Now + ShopAuthorization.ValidFor - TimeSpan.FromSeconds(1)).ShouldBeTrue();
        authorization.CanReceiveKeysAt(Now + ShopAuthorization.ValidFor).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => authorization.ReceiveKeys("sifrovano", Now + ShopAuthorization.ValidFor));
        authorization.HasKeys.ShouldBeFalse();
    }
}
