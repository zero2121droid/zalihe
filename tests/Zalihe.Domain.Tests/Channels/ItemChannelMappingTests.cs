using Shouldly;
using Zalihe.Domain.Channels;
using Zalihe.Domain.Items;

namespace Zalihe.Domain.Tests.Channels;

public class ItemChannelMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private static SalesChannel Channel(Guid tenantId) =>
        new(tenantId, SalesChannelType.WooCommerce, "https://zrno.rs", "enc-credentials", "enc-secret", Now);

    private static Item Item(Guid tenantId) => new(tenantId, "Majica basic – M, Siva", "MAJ-M-SI", Unit.Kom, 0, Now);

    [Fact]
    public void Constructor_Variation_KeepsBothShopIds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var item = Item(tenantId);
        var channel = Channel(tenantId);

        // Act
        var mapping = new ItemChannelMapping(item, channel, " 57 ", "50", Now);

        // Assert
        mapping.TenantId.ShouldBe(tenantId);
        mapping.ItemId.ShouldBe(item.Id);
        mapping.ChannelId.ShouldBe(channel.Id);
        mapping.ExternalId.ShouldBe("57");
        mapping.ParentExternalId.ShouldBe("50");
    }

    [Fact]
    public void Constructor_SimpleProduct_HasNoParent()
    {
        var tenantId = Guid.NewGuid();

        new ItemChannelMapping(Item(tenantId), Channel(tenantId), "12", "  ", Now).ParentExternalId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ItemOfAnotherCompany_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            new ItemChannelMapping(Item(Guid.NewGuid()), Channel(Guid.NewGuid()), "12", null, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_MissingExternalId_Throws(string externalId)
    {
        var tenantId = Guid.NewGuid();

        Should.Throw<ArgumentException>(() => new ItemChannelMapping(Item(tenantId), Channel(tenantId), externalId, null, Now));
    }

    [Theory]
    [InlineData(24, 24)]
    [InlineData(2.5, 2)]
    [InlineData(0.999, 0)]
    [InlineData(-0.5, -1)]
    public void ShopStock_Quantity_IsRoundedDownToWholeUnits(decimal stock, int expected)
    {
        ItemChannelMapping.ShopStock(stock).ShouldBe(expected);
    }

    [Fact]
    public void MarkStockChanged_NewMapping_RemembersWhen()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mapping = new ItemChannelMapping(Item(tenantId), Channel(tenantId), "57", null, Now);

        // Act
        mapping.MarkStockChanged(Now.AddMinutes(1));

        // Assert
        mapping.StockChangedAt.ShouldBe(Now.AddMinutes(1));
        mapping.PushedStock.ShouldBeNull();
    }

    [Theory]
    [InlineData("https://zrno.rs/wp-content/uploads/kafa.jpeg", "https://zrno.rs/wp-content/uploads/kafa.jpeg")]
    [InlineData(" http://localhost:8080/wp-content/uploads/majica.jpeg ", "http://localhost:8080/wp-content/uploads/majica.jpeg")]
    [InlineData("https://zrno.rs/uploads/šolja 1.jpg", "https://zrno.rs/uploads/%C5%A1olja%201.jpg")]
    public void SetImageUrl_ShopAddress_KeepsIt(string url, string expected)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mapping = new ItemChannelMapping(Item(tenantId), Channel(tenantId), "12", null, Now);

        // Act
        mapping.SetImageUrl(url);

        // Assert
        mapping.ImageUrl.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/wp-content/uploads/kafa.jpeg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("ftp://zrno.rs/kafa.jpeg")]
    public void SetImageUrl_NotAnHttpAddress_ClearsTheImage(string? url)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mapping = new ItemChannelMapping(Item(tenantId), Channel(tenantId), "12", null, Now);
        mapping.SetImageUrl("https://zrno.rs/kafa.jpeg");

        // Act
        mapping.SetImageUrl(url);

        // Assert
        mapping.ImageUrl.ShouldBeNull();
    }

    [Fact]
    public void SetImageUrl_TooLong_ClearsTheImage()
    {
        var tenantId = Guid.NewGuid();
        var mapping = new ItemChannelMapping(Item(tenantId), Channel(tenantId), "12", null, Now);

        mapping.SetImageUrl("https://zrno.rs/" + new string('a', ItemChannelMapping.ImageUrlMaxLength));

        mapping.ImageUrl.ShouldBeNull();
    }
}
