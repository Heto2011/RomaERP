using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class PhysicalStockCountServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_SnapshotsSystemQuantityAndComputesVariance()
    {
        var ctx = CreateContext();
        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var item = new Item { Code = "ITM-A", NameAr = "صنف أ", NameEn = "Item A", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 100, AverageCost = 5 };
        ctx.ItemCategories.Add(category);
        ctx.Items.Add(item);
        await ctx.SaveChangesAsync();

        var service = new PhysicalStockCountService(ctx);
        var result = await service.CreateAsync(new CreatePhysicalStockCountDto
        {
            ItemId = item.Id,
            CountDate = new DateTime(2026, 8, 15),
            CountedQuantity = 92,
            Notes = "جرد شهري"
        });

        Assert.Equal(100, result.SystemQuantity);
        Assert.Equal(92, result.CountedQuantity);
        Assert.Equal(-8, result.Variance);
        Assert.Equal(-40, result.VarianceValue); // -8 * 5
    }

    [Fact]
    public async Task CreateAsync_DoesNotChangeItemQuantityOnHand()
    {
        var ctx = CreateContext();
        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var item = new Item { Code = "ITM-A", NameAr = "صنف أ", NameEn = "Item A", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 100, AverageCost = 5 };
        ctx.ItemCategories.Add(category);
        ctx.Items.Add(item);
        await ctx.SaveChangesAsync();

        var service = new PhysicalStockCountService(ctx);
        await service.CreateAsync(new CreatePhysicalStockCountDto { ItemId = item.Id, CountDate = new DateTime(2026, 8, 15), CountedQuantity = 50 });

        var reloaded = await ctx.Items.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        Assert.Equal(100, reloaded.QuantityOnHand);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndExcludesFromGetAll()
    {
        var ctx = CreateContext();
        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var item = new Item { Code = "ITM-A", NameAr = "صنف أ", NameEn = "Item A", UnitOfMeasure = "قطعة", ItemCategory = category };
        ctx.ItemCategories.Add(category);
        ctx.Items.Add(item);
        await ctx.SaveChangesAsync();

        var service = new PhysicalStockCountService(ctx);
        var created = await service.CreateAsync(new CreatePhysicalStockCountDto { ItemId = item.Id, CountDate = new DateTime(2026, 8, 15), CountedQuantity = 10 });

        await service.DeleteAsync(created.Id);

        var all = await service.GetAllAsync();
        Assert.Empty(all);
    }
}
