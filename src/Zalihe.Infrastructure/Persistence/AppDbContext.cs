using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Identity;

namespace Zalihe.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Item> Items => Set<Item>();

    /// <summary>
    /// Read by the global query filters on every query. Throws when no tenant is set,
    /// so company data can never be read for "no company".
    /// </summary>
    private Guid CurrentTenantId => tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(tenant =>
        {
            tenant.Property(t => t.Name).HasMaxLength(Tenant.NameMaxLength);
        });

        // Users are not company data in the filter sense: sign-in looks them up by email
        // before the company is known.
        builder.Entity<User>(user =>
        {
            user.Property(u => u.Name).HasMaxLength(User.NameMaxLength);
            user.Property(u => u.Language).HasMaxLength(Languages.MaxLength);
            user.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
            user.HasIndex(u => u.TenantId);
        });

        builder.Entity<Item>(item =>
        {
            item.Property(i => i.Name).HasMaxLength(Item.NameMaxLength);
            item.Property(i => i.Sku).HasMaxLength(Item.SkuMaxLength);
            item.Property(i => i.Barcode).HasMaxLength(Item.BarcodeMaxLength);
            item.Property(i => i.Category).HasMaxLength(Item.CategoryMaxLength);
            item.Property(i => i.GroupName).HasMaxLength(Item.GroupNameMaxLength);
            item.Property(i => i.Unit)
                .HasMaxLength(8)
                .HasConversion(u => u.ToString().ToLowerInvariant(), s => Enum.Parse<Unit>(s, ignoreCase: true));
            item.Property(i => i.PurchasePrice).HasPrecision(18, 2);
            item.Property(i => i.SalePrice).HasPrecision(18, 2);
            item.Property(i => i.MinStock).HasPrecision(18, 3);
            item.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);
            item.HasIndex(i => new { i.TenantId, i.Sku }).IsUnique();
            item.HasIndex(i => new { i.TenantId, i.Name });
        });

        ApplyTenantFilters(builder);
    }

    /// <summary>Adds "TenantId == current tenant" to every entity implementing <see cref="ITenantOwned"/>.</summary>
    private void ApplyTenantFilters(ModelBuilder builder)
    {
        var apply = typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                apply.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
            }
        }
    }

    private void ApplyTenantFilter<T>(ModelBuilder builder) where T : class, ITenantOwned =>
        builder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        EnsureNewDataBelongsToCurrentTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureNewDataBelongsToCurrentTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    // Last line of defence: a bug can't write one company's data into another company.
    private void EnsureNewDataBelongsToCurrentTenant()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId != CurrentTenantId)
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} belongs to a different tenant than the current one.");
            }
        }
    }
}
