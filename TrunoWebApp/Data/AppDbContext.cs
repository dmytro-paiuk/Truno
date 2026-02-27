using Microsoft.EntityFrameworkCore;
using TrunoWebApp.Data.Entities;

namespace TrunoWebApp.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TruCommerceItemEntity> TruCommerceItems => Set<TruCommerceItemEntity>();
    public DbSet<TruCommerceRegPriceEntity> TruCommerceRegPrices => Set<TruCommerceRegPriceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TruCommerceItemEntity>(e =>
        {
            e.ToTable("TruCommerceItems");

            e.HasKey(x => x.Id);

            // Very important: API provides Id, so don't let SQL Server treat it as IDENTITY.
            e.Property(x => x.Id).ValueGeneratedNever();

            e.Property(x => x.UpcEAN).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(400);
            e.Property(x => x.BrandName).HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(200);

            e.Property(x => x.EffectiveUnitPrice).HasColumnType("decimal(18,4)");
            e.Property(x => x.EffectiveCasePrice).HasColumnType("decimal(18,4)");

            e.Property(x => x.RawJson).IsRequired();
            e.Property(x => x.SyncedUtc).IsRequired();

            // Relationship configured on reg prices side too, but either place is fine.
            e.HasMany(x => x.RegPrices)
                .WithOne(x => x.Item)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TruCommerceRegPriceEntity>(e =>
        {
            e.ToTable("TruCommerceRegPrices");

            e.HasKey(x => new { x.ItemId, x.RegPriceId });

            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(x => x.CasePrice).HasColumnType("decimal(18,4)");

            e.Property(x => x.RawJson).IsRequired();
            e.Property(x => x.SyncedUtc).IsRequired();

            // Optional: helpful index for common queries
            e.HasIndex(x => x.ItemId);
        });
    }
}