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
}
