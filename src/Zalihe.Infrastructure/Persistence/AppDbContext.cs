using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Channels;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;
using Zalihe.Domain.Tenants;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Identity;

namespace Zalihe.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemChange> ItemChanges => Set<ItemChange>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<SalesChannel> SalesChannels => Set<SalesChannel>();

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

        builder.Entity<ItemChange>(change =>
        {
            change.Property(c => c.Kind).HasConversion<string>().HasMaxLength(16);
            // Changed fields are stored as one JSON document per entry.
            change.OwnsMany(c => c.Changes, field =>
            {
                field.ToJson();
                field.Property(f => f.Field).HasMaxLength(32);
            });
            change.HasOne<Item>().WithMany().HasForeignKey(c => c.ItemId).OnDelete(DeleteBehavior.Restrict);
            change.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
            change.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
            change.HasIndex(c => new { c.ItemId, c.OccurredAt });
        });

        builder.Entity<StockMovement>(movement =>
        {
            movement.Property(m => m.Type).HasConversion<string>().HasMaxLength(16);
            movement.Property(m => m.Source).HasConversion<string>().HasMaxLength(16);
            movement.Property(m => m.Quantity).HasPrecision(18, 3);
            movement.Property(m => m.Note).HasMaxLength(StockMovement.NoteMaxLength);
            movement.Property(m => m.ExternalRef).HasMaxLength(StockMovement.ExternalRefMaxLength);
            movement.HasOne<Item>().WithMany().HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
            // History per item (newest first) and "sold in the last 30 days".
            movement.HasIndex(m => new { m.ItemId, m.OccurredAt });
            movement.HasIndex(m => new { m.TenantId, m.Type, m.OccurredAt });
        });

        builder.Entity<StockLevel>(level =>
        {
            level.HasKey(l => l.ItemId);
            level.Property(l => l.Quantity).HasPrecision(18, 3);
            // Maps to PostgreSQL's xmin system column: a concurrent update fails instead of being lost.
            level.Property(l => l.RowVersion).IsRowVersion();
            level.HasOne<Item>().WithOne().HasForeignKey<StockLevel>(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
            level.HasOne<Tenant>().WithMany().HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Restrict);
            level.HasIndex(l => l.TenantId);
        });

        builder.Entity<SalesChannel>(channel =>
        {
            channel.Property(c => c.Type).HasConversion<string>().HasMaxLength(32);
            channel.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            channel.Property(c => c.BaseUrl).HasMaxLength(SalesChannel.BaseUrlMaxLength);
            channel.Property(c => c.LastErrorCode).HasMaxLength(64);
            channel.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
            // One shop of each type per company in v1.
            channel.HasIndex(c => new { c.TenantId, c.Type }).IsUnique();
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

    public Task<StockLevel?> LockStockLevelAsync(Guid itemId, CancellationToken ct) =>
        // FOR UPDATE makes other transactions wait for this row. xmin (the row version) is a system
        // column, so SELECT * leaves it out and it is listed explicitly. The tenant filter still applies,
        // because EF composes the global query filter around this SQL.
        StockLevels
            .FromSql($"""SELECT *, xmin FROM "StockLevels" WHERE "ItemId" = {itemId} FOR UPDATE""")
            .SingleOrDefaultAsync(ct);

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
