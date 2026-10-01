using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zalihe.Domain.Tenants;
using Zalihe.Infrastructure.Identity;
using Zalihe.Infrastructure.Persistence;
using Zalihe.IntegrationTests.Infrastructure;

namespace Zalihe.IntegrationTests.Tenancy;

/// <summary>
/// Guards the rule "every table with company data has TenantId and a global query filter".
/// Fails as soon as a new entity is added without it.
/// </summary>
[Collection(ApiCollection.Name)]
public class TenantFilterTests(ZaliheApiFactory factory)
{
    // Users are looked up by email at sign-in, before the company is known.
    private static readonly Type[] NotFiltered = [typeof(User)];

    [Fact]
    public void Model_EntitiesWithTenantId_ImplementITenantOwnedAndHaveQueryFilter()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;

        // Act
        var entities = model.GetEntityTypes()
            .Where(e => e.FindProperty(nameof(ITenantOwned.TenantId)) is not null)
            .Where(e => !NotFiltered.Contains(e.ClrType))
            .ToList();

        // Assert
        entities.ShouldNotBeEmpty();
        entities.ShouldAllBe(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType));
        entities.ShouldAllBe(e => e.GetDeclaredQueryFilters().Count > 0);
    }

    [Fact]
    public async Task Query_NoTenantSet_ThrowsInsteadOfReturningData()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => db.Items.ToListAsync());
    }
}
