using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ItemLotServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private record SeedResult(ApplicationDbContext Ctx, Warehouse Warehouse, Item LotTracked, Item Untracked);

    private static async Task<SeedResult> SeedAsync()
    {
        var ctx = CreateContext();
        var warehouse = new Warehouse { Code = "WH-1", NameAr = "المخزن", NameEn = "Warehouse" };
        var category = new ItemCategory { Code = "CAT-1", NameAr = "تصنيف", NameEn = "Category" };
        var lotTracked = new Item { Code = "DAIRY-1", NameAr = "لبن", NameEn = "Milk", UnitOfMeasure = "لتر", ItemCategoryId = category.Id, IsLotTracked = true, QuantityOnHand = 0, AverageCost = 0 };
        var untracked = new Item { Code = "RM-SALT", NameAr = "ملح", NameEn = "Salt", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, IsLotTracked = false, QuantityOnHand = 100, AverageCost = 5 };

        ctx.Warehouses.Add(warehouse);
        ctx.ItemCategories.Add(category);
        ctx.Items.AddRange(lotTracked, untracked);
        await ctx.SaveChangesAsync();

        return new SeedResult(ctx, warehouse, lotTracked, untracked);
    }

    [Fact]
    public async Task ReceiveLotAsync_ForUntrackedItem_IsNoOp()
    {
        var seed = await SeedAsync();
        var service = new ItemLotService(seed.Ctx);

        await service.ReceiveLotAsync(seed.Untracked.Id, seed.Warehouse.Id, lotNumber: null, quantity: 10, unitCost: 5, expiryDate: null, receivedDate: DateTime.UtcNow.Date);

        Assert.Empty(await seed.Ctx.ItemLots.ToListAsync());
    }

    [Fact]
    public async Task ReceiveLotAsync_ForLotTrackedItem_RequiresLotNumber()
    {
        var seed = await SeedAsync();
        var service = new ItemLotService(seed.Ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.ReceiveLotAsync(seed.LotTracked.Id, seed.Warehouse.Id, lotNumber: null, quantity: 10, unitCost: 8, expiryDate: DateTime.UtcNow.Date.AddDays(5), receivedDate: DateTime.UtcNow.Date));
    }

    [Fact]
    public async Task ConsumeFefoAsync_ConsumesEarliestExpiryFirst()
    {
        var seed = await SeedAsync();
        var service = new ItemLotService(seed.Ctx);
        var today = DateTime.UtcNow.Date;

        await service.ReceiveLotAsync(seed.LotTracked.Id, seed.Warehouse.Id, "LOT-A", 10, 8, today.AddDays(10), today);
        await service.ReceiveLotAsync(seed.LotTracked.Id, seed.Warehouse.Id, "LOT-B", 10, 9, today.AddDays(2), today);
        await seed.Ctx.SaveChangesAsync();

        // Consuming 12 should fully drain LOT-B (expires first, 10 units) then take 2 from LOT-A.
        await service.ConsumeFefoAsync(seed.LotTracked.Id, seed.Warehouse.Id, 12);
        await seed.Ctx.SaveChangesAsync();

        var lots = await seed.Ctx.ItemLots.OrderBy(l => l.LotNumber).ToListAsync();
        var lotA = lots.First(l => l.LotNumber == "LOT-A");
        var lotB = lots.First(l => l.LotNumber == "LOT-B");
        Assert.Equal(8, lotA.QuantityOnHand);
        Assert.Equal(0, lotB.QuantityOnHand);
    }

    [Fact]
    public async Task GetExpiringLotsAsync_ReturnsOnlyLotsWithinWindowAndFlagsExpired()
    {
        var seed = await SeedAsync();
        var service = new ItemLotService(seed.Ctx);
        var today = DateTime.UtcNow.Date;

        await service.ReceiveLotAsync(seed.LotTracked.Id, seed.Warehouse.Id, "LOT-EXPIRED", 5, 8, today.AddDays(-1), today.AddDays(-10));
        await service.ReceiveLotAsync(seed.LotTracked.Id, seed.Warehouse.Id, "LOT-SOON", 5, 8, today.AddDays(3), today);
        await service.ReceiveLotAsync(seed.LotTracked.Id, seed.Warehouse.Id, "LOT-FAR", 5, 8, today.AddDays(60), today);
        await seed.Ctx.SaveChangesAsync();

        var expiring = await service.GetExpiringLotsAsync(withinDays: 7);

        Assert.Equal(2, expiring.Count);
        Assert.Contains(expiring, l => l.LotNumber == "LOT-EXPIRED" && l.IsExpired);
        Assert.Contains(expiring, l => l.LotNumber == "LOT-SOON" && !l.IsExpired);
        Assert.DoesNotContain(expiring, l => l.LotNumber == "LOT-FAR");
    }
}
