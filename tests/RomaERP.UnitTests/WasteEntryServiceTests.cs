using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class WasteEntryServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Item item, Warehouse warehouse, FiscalPeriod period)> SeedAsync()
    {
        var ctx = CreateContext();
        var today = new DateTime(2026, 8, 15);

        var inventoryAccount = new Account { Code = AccountingConstants.InventoryAccountCode, NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var cogsAccount = new Account { Code = AccountingConstants.CostOfGoodsSoldAccountCode, NameAr = "تكلفة البضاعة المباعة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        ctx.Accounts.AddRange(inventoryAccount, cogsAccount);

        var year = new FiscalYear { Name = "2026", StartDate = today.AddMonths(-6), EndDate = today.AddMonths(6) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);

        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var item = new Item { Code = "ITM-A", NameAr = "صنف أ", NameEn = "Item A", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 50, AverageCost = 8 };
        var warehouse = new Warehouse { Code = "WH-MAIN", NameAr = "المخزن الرئيسي", NameEn = "Main Warehouse" };
        ctx.ItemCategories.Add(category);
        ctx.Items.Add(item);
        ctx.Warehouses.Add(warehouse);

        await ctx.SaveChangesAsync();
        return (ctx, item, warehouse, period);
    }

    [Fact]
    public async Task CreateAsync_IssuesRealStockMovementAndReducesQuantityOnHand()
    {
        var (ctx, item, warehouse, period) = await SeedAsync();
        var inventoryService = new InventoryService(ctx);
        var service = new WasteEntryService(ctx, inventoryService);

        var result = await service.CreateAsync(new CreateWasteEntryDto
        {
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            FiscalPeriodId = period.Id,
            WasteDate = new DateTime(2026, 8, 15),
            Quantity = 5,
            Reason = (int)WasteReason.Expired,
            Notes = "منتهي الصلاحية"
        });

        Assert.Equal(8, result.UnitCost);
        Assert.Equal(40, result.TotalCost);

        var updatedItem = await ctx.Items.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        Assert.Equal(45, updatedItem.QuantityOnHand);

        var movement = await ctx.StockMovements.AsNoTracking().SingleAsync(m => m.ItemId == item.Id);
        Assert.Equal(StockMovementType.Issue, movement.MovementType);
        Assert.Equal("WASTE", movement.Reference);

        var journalEntry = await ctx.JournalEntries.AsNoTracking().FirstAsync(j => j.Id == movement.JournalEntryId);
        Assert.Equal(JournalEntryStatus.Posted, journalEntry.Status);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCreatedEntriesWithReason()
    {
        var (ctx, item, warehouse, period) = await SeedAsync();
        var inventoryService = new InventoryService(ctx);
        var service = new WasteEntryService(ctx, inventoryService);

        await service.CreateAsync(new CreateWasteEntryDto
        {
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            FiscalPeriodId = period.Id,
            WasteDate = new DateTime(2026, 8, 15),
            Quantity = 2,
            Reason = (int)WasteReason.Damaged
        });

        var all = await service.GetAllAsync();
        var entry = Assert.Single(all);
        Assert.Equal((int)WasteReason.Damaged, entry.Reason);
        Assert.Equal(16, entry.TotalCost);
    }
}
