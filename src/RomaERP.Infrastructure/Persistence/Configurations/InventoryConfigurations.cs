using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Inventory;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class ItemCategoryConfiguration : IEntityTypeConfiguration<ItemCategory>
{
    public void Configure(EntityTypeBuilder<ItemCategory> builder)
    {
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.Property(w => w.Code).HasMaxLength(20).IsRequired();
        builder.Property(w => w.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(w => w.NameEn).HasMaxLength(200).IsRequired();
        builder.HasIndex(w => w.Code).IsUnique();
        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.Property(i => i.Code).HasMaxLength(30).IsRequired();
        builder.Property(i => i.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(i => i.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(i => i.UnitOfMeasure).HasMaxLength(20).IsRequired();
        builder.Property(i => i.ReorderLevel).HasPrecision(18, 4);
        builder.Property(i => i.QuantityOnHand).HasPrecision(18, 4);
        builder.Property(i => i.AverageCost).HasPrecision(18, 4);
        builder.Property(i => i.MenuPrice).HasPrecision(18, 2);
        builder.HasIndex(i => i.Code).IsUnique();

        builder.HasOne(i => i.ItemCategory)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.ItemCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.Property(m => m.MovementNumber).HasMaxLength(30).IsRequired();
        builder.Property(m => m.Quantity).HasPrecision(18, 4);
        builder.Property(m => m.UnitCost).HasPrecision(18, 4);
        builder.Property(m => m.TotalCost).HasPrecision(18, 2);
        builder.Property(m => m.Reference).HasMaxLength(100);
        builder.Property(m => m.Description).HasMaxLength(500);
        builder.HasIndex(m => m.MovementNumber).IsUnique();

        builder.HasOne(m => m.Item)
            .WithMany(i => i.Movements)
            .HasForeignKey(m => m.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Warehouse)
            .WithMany()
            .HasForeignKey(m => m.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.CostCenter)
            .WithMany()
            .HasForeignKey(m => m.CostCenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.JournalEntry)
            .WithMany()
            .HasForeignKey(m => m.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}

public class PhysicalStockCountConfiguration : IEntityTypeConfiguration<PhysicalStockCount>
{
    public void Configure(EntityTypeBuilder<PhysicalStockCount> builder)
    {
        builder.Property(c => c.SystemQuantity).HasPrecision(18, 4);
        builder.Property(c => c.CountedQuantity).HasPrecision(18, 4);
        builder.Property(c => c.UnitCost).HasPrecision(18, 4);
        builder.Property(c => c.Notes).HasMaxLength(500);

        builder.HasOne(c => c.Item)
            .WithMany()
            .HasForeignKey(c => c.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class WasteEntryConfiguration : IEntityTypeConfiguration<WasteEntry>
{
    public void Configure(EntityTypeBuilder<WasteEntry> builder)
    {
        builder.Property(w => w.Quantity).HasPrecision(18, 4);
        builder.Property(w => w.UnitCost).HasPrecision(18, 4);
        builder.Property(w => w.TotalCost).HasPrecision(18, 2);
        builder.Property(w => w.Notes).HasMaxLength(500);

        builder.HasOne(w => w.Item)
            .WithMany()
            .HasForeignKey(w => w.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.StockMovement)
            .WithMany()
            .HasForeignKey(w => w.StockMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
