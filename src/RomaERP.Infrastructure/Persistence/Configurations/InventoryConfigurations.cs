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

public class ManufacturingBomConfiguration : IEntityTypeConfiguration<ManufacturingBom>
{
    public void Configure(EntityTypeBuilder<ManufacturingBom> builder)
    {
        builder.Property(b => b.OutputQuantity).HasPrecision(18, 4);
        builder.HasIndex(b => b.OutputItemId).IsUnique();

        builder.HasOne(b => b.OutputItem)
            .WithMany()
            .HasForeignKey(b => b.OutputItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}

public class ManufacturingBomLineConfiguration : IEntityTypeConfiguration<ManufacturingBomLine>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomLine> builder)
    {
        builder.Property(l => l.QuantityPerBatch).HasPrecision(18, 4);

        builder.HasOne(l => l.Bom)
            .WithMany(b => b.Lines)
            .HasForeignKey(l => l.BomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.RawMaterialItem)
            .WithMany()
            .HasForeignKey(l => l.RawMaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ManufacturingOrderConfiguration : IEntityTypeConfiguration<ManufacturingOrder>
{
    public void Configure(EntityTypeBuilder<ManufacturingOrder> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.ProducedQuantity).HasPrecision(18, 4);
        builder.Property(o => o.TotalCost).HasPrecision(18, 2);
        builder.Property(o => o.Notes).HasMaxLength(500);
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.HasOne(o => o.Bom)
            .WithMany()
            .HasForeignKey(o => o.BomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Warehouse)
            .WithMany()
            .HasForeignKey(o => o.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}

public class ManufacturingOrderLineConfiguration : IEntityTypeConfiguration<ManufacturingOrderLine>
{
    public void Configure(EntityTypeBuilder<ManufacturingOrderLine> builder)
    {
        builder.Property(l => l.QuantityConsumed).HasPrecision(18, 4);
        builder.Property(l => l.UnitCost).HasPrecision(18, 4);
        builder.Property(l => l.TotalCost).HasPrecision(18, 2);

        builder.HasOne(l => l.ManufacturingOrder)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.ManufacturingOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.RawMaterialItem)
            .WithMany()
            .HasForeignKey(l => l.RawMaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ItemLotConfiguration : IEntityTypeConfiguration<ItemLot>
{
    public void Configure(EntityTypeBuilder<ItemLot> builder)
    {
        builder.Property(l => l.LotNumber).HasMaxLength(60).IsRequired();
        builder.Property(l => l.QuantityOnHand).HasPrecision(18, 4);
        builder.Property(l => l.UnitCost).HasPrecision(18, 4);
        builder.HasIndex(l => new { l.ItemId, l.WarehouseId, l.LotNumber }).IsUnique();

        builder.HasOne(l => l.Item)
            .WithMany(i => i.Lots)
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Warehouse)
            .WithMany()
            .HasForeignKey(l => l.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
