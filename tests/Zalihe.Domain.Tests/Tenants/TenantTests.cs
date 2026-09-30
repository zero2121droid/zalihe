using Shouldly;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Tests.Tenants;

public class TenantTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_NameWithSurroundingWhitespace_TrimsName()
    {
        // Act
        var tenant = new Tenant("  Moja Firma  ", Now);

        // Assert
        tenant.Name.ShouldBe("Moja Firma");
        tenant.CreatedAt.ShouldBe(Now);
        tenant.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_Throws(string name)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new Tenant(name, Now));
    }

    [Fact]
    public void Constructor_NameTooLong_Throws()
    {
        // Arrange
        var name = new string('a', Tenant.NameMaxLength + 1);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new Tenant(name, Now));
    }
}
