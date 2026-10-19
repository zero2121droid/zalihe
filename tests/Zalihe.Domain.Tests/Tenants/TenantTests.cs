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

    [Fact]
    public void Rename_NewName_TrimsAndKeepsTheRest()
    {
        // Arrange
        var tenant = new Tenant("Moja Firma", Now);
        var id = tenant.Id;

        // Act
        tenant.Rename("  Zrno d.o.o.  ");

        // Assert
        tenant.Name.ShouldBe("Zrno d.o.o.");
        tenant.Id.ShouldBe(id);
        tenant.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_EmptyName_ThrowsAndKeepsTheOldName(string name)
    {
        // Arrange
        var tenant = new Tenant("Moja Firma", Now);

        // Act
        Should.Throw<ArgumentException>(() => tenant.Rename(name));

        // Assert
        tenant.Name.ShouldBe("Moja Firma");
    }

    [Fact]
    public void Rename_NameTooLong_Throws()
    {
        var tenant = new Tenant("Moja Firma", Now);

        Should.Throw<ArgumentOutOfRangeException>(() => tenant.Rename(new string('a', Tenant.NameMaxLength + 1)));
    }
}
