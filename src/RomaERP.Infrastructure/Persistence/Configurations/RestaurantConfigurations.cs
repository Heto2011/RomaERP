using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Restaurant;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class RestaurantTableConfiguration : IEntityTypeConfiguration<RestaurantTable>
{
    public void Configure(EntityTypeBuilder<RestaurantTable> builder)
    {
        builder.Property(t => t.Number).HasMaxLength(20).IsRequired();
        builder.Property(t => t.SectionName).HasMaxLength(100);
        builder.HasIndex(t => t.Number).IsUnique();

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

public class RestaurantOrderConfiguration : IEntityTypeConfiguration<RestaurantOrder>
{
    public void Configure(EntityTypeBuilder<RestaurantOrder> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        builder.Property(o => o.CustomerName).HasMaxLength(200);
        builder.Property(o => o.CustomerPhone).HasMaxLength(30);
        builder.Property(o => o.DeliveryAddress).HasMaxLength(500);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.HasOne(o => o.Table)
            .WithMany()
            .HasForeignKey(o => o.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.WaiterEmployee)
            .WithMany()
            .HasForeignKey(o => o.WaiterEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Warehouse)
            .WithMany()
            .HasForeignKey(o => o.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.SalesInvoice)
            .WithMany()
            .HasForeignKey(o => o.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Lines)
            .WithOne(l => l.RestaurantOrder)
            .HasForeignKey(l => l.RestaurantOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}

public class RestaurantOrderLineConfiguration : IEntityTypeConfiguration<RestaurantOrderLine>
{
    public void Configure(EntityTypeBuilder<RestaurantOrderLine> builder)
    {
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MenuRecipeLineConfiguration : IEntityTypeConfiguration<MenuRecipeLine>
{
    public void Configure(EntityTypeBuilder<MenuRecipeLine> builder)
    {
        builder.Property(l => l.QuantityPerUnit).HasPrecision(18, 4);

        builder.HasOne(l => l.MenuItem)
            .WithMany(i => i.RecipeLines)
            .HasForeignKey(l => l.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.RawMaterialItem)
            .WithMany()
            .HasForeignKey(l => l.RawMaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.MenuItemId, l.RawMaterialItemId }).IsUnique();
    }
}
