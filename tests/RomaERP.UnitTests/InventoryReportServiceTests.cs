using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Restaurant;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class InventoryReportServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetStockValuation_ComputesValueAndSkipsZeroQuantityItems()
    {
        var ctx = CreateContext();
        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var stocked = new Item { Code = "ITM-A", NameAr = "صنف أ", NameEn = "Item A", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 10, AverageCost = 5 };
        var empty = new Item { Code = "ITM-B", NameAr = "صنف ب", NameEn = "Item B", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 0, AverageCost = 8 };

        ctx.ItemCategories.Add(category);
        ctx.Items.AddRange(stocked, empty);
        await ctx.SaveChangesAsync();

        var service = new InventoryReportService(ctx);
        var report = await service.GetStockValuationAsync();

        Assert.Single(report.Items);
        Assert.Equal("ITM-A", report.Items[0].ItemCode);
        Assert.Equal(50, report.Items[0].Value);
        Assert.Equal(50, report.TotalValue);
    }

    [Fact]
    public async Task GetInventoryMovement_FlagsDeadStockAtRiskAndExcessStock()
    {
        var ctx = CreateContext();
        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var warehouse = new Warehouse { Code = "WH-MAIN", NameAr = "المخزن الرئيسي", NameEn = "Main Warehouse" };

        // Dead stock: quantity on hand, zero issues in the period.
        var dead = new Item { Code = "ITM-DEAD", NameAr = "صنف راكد", NameEn = "Dead Item", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 20, AverageCost = 10, ReorderLevel = 5 };
        // At risk: quantity on hand below its configured reorder level.
        var atRisk = new Item { Code = "ITM-RISK", NameAr = "صنف قارب على النفاد", NameEn = "At Risk Item", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 2, AverageCost = 10, ReorderLevel = 10 };
        // Excess: huge quantity on hand relative to its (small) usage rate -> days-of-stock far above threshold.
        var excess = new Item { Code = "ITM-EXCESS", NameAr = "صنف زيادة", NameEn = "Excess Item", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 1000, AverageCost = 2, ReorderLevel = 0 };

        ctx.ItemCategories.Add(category);
        ctx.Warehouses.Add(warehouse);
        ctx.Items.AddRange(dead, atRisk, excess);
        await ctx.SaveChangesAsync();

        ctx.StockMovements.Add(new StockMovement
        {
            MovementNumber = "SM-000001",
            MovementDate = new DateTime(2026, 8, 10),
            MovementType = StockMovementType.Issue,
            ItemId = excess.Id,
            WarehouseId = warehouse.Id,
            Quantity = 1,
            UnitCost = 2,
            TotalCost = 2
        });
        await ctx.SaveChangesAsync();

        var service = new InventoryReportService(ctx);
        var report = await service.GetInventoryMovementAsync(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        var deadLine = report.Items.Single(l => l.ItemCode == "ITM-DEAD");
        Assert.True(deadLine.IsDeadStock);
        Assert.False(deadLine.IsAtRiskOfStockout);

        var riskLine = report.Items.Single(l => l.ItemCode == "ITM-RISK");
        Assert.True(riskLine.IsAtRiskOfStockout);
        // No movements at all in the period -> counts as dead stock too (zero usage), which is honest.
        Assert.True(riskLine.IsDeadStock);

        var excessLine = report.Items.Single(l => l.ItemCode == "ITM-EXCESS");
        Assert.False(excessLine.IsDeadStock);
        Assert.True(excessLine.IsExcessStock);
        Assert.NotNull(excessLine.DaysOfStockRemaining);
        Assert.True(excessLine.DaysOfStockRemaining > 90);
    }

    [Fact]
    public async Task GetPurchasePriceVariance_ComparesLatestReceiptAgainstPriorAndSkipsSingleReceiptItems()
    {
        var ctx = CreateContext();
        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var warehouse = new Warehouse { Code = "WH-MAIN", NameAr = "المخزن الرئيسي", NameEn = "Main Warehouse" };
        var itemWithHistory = new Item { Code = "ITM-A", NameAr = "صنف له تاريخ شراء", NameEn = "Item With History", UnitOfMeasure = "قطعة", ItemCategory = category };
        var itemSingleReceipt = new Item { Code = "ITM-B", NameAr = "صنف باستلام واحد", NameEn = "Single Receipt Item", UnitOfMeasure = "قطعة", ItemCategory = category };

        ctx.ItemCategories.Add(category);
        ctx.Warehouses.Add(warehouse);
        ctx.Items.AddRange(itemWithHistory, itemSingleReceipt);
        await ctx.SaveChangesAsync();

        ctx.StockMovements.AddRange(
            new StockMovement { MovementNumber = "SM-1", MovementDate = new DateTime(2026, 7, 1), MovementType = StockMovementType.Receipt, ItemId = itemWithHistory.Id, WarehouseId = warehouse.Id, Quantity = 10, UnitCost = 20, TotalCost = 200 },
            new StockMovement { MovementNumber = "SM-2", MovementDate = new DateTime(2026, 8, 15), MovementType = StockMovementType.Receipt, ItemId = itemWithHistory.Id, WarehouseId = warehouse.Id, Quantity = 10, UnitCost = 25, TotalCost = 250 },
            new StockMovement { MovementNumber = "SM-3", MovementDate = new DateTime(2026, 8, 5), MovementType = StockMovementType.Receipt, ItemId = itemSingleReceipt.Id, WarehouseId = warehouse.Id, Quantity = 5, UnitCost = 30, TotalCost = 150 }
        );
        await ctx.SaveChangesAsync();

        var service = new InventoryReportService(ctx);
        var report = await service.GetPurchasePriceVarianceAsync(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        var line = Assert.Single(report.Items);
        Assert.Equal("ITM-A", line.ItemCode);
        Assert.Equal(20, line.PreviousUnitCost);
        Assert.Equal(25, line.LatestUnitCost);
        Assert.Equal(5, line.ChangeAmount);
        Assert.Equal(25, line.ChangePercent);
    }

    [Fact]
    public async Task GetRecipeCost_UsesBomForRecipeItemsAndOwnAverageCostOtherwise()
    {
        var ctx = CreateContext();
        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var rawMaterial = new Item { Code = "RAW-1", NameAr = "خبز", NameEn = "Bread", UnitOfMeasure = "قطعة", ItemCategory = category, AverageCost = 2 };
        var burger = new Item { Code = "MENU-1", NameAr = "برجر", NameEn = "Burger", UnitOfMeasure = "قطعة", ItemCategory = category, IsMenuItem = true, MenuPrice = 50, AverageCost = 999 };
        var water = new Item { Code = "MENU-2", NameAr = "مياه", NameEn = "Water", UnitOfMeasure = "قطعة", ItemCategory = category, IsMenuItem = true, MenuPrice = 10, AverageCost = 3 };

        ctx.ItemCategories.Add(category);
        ctx.Items.AddRange(rawMaterial, burger, water);
        ctx.MenuRecipeLines.Add(new MenuRecipeLine { MenuItemId = burger.Id, RawMaterialItemId = rawMaterial.Id, QuantityPerUnit = 2 });
        await ctx.SaveChangesAsync();

        var service = new InventoryReportService(ctx);
        var report = await service.GetRecipeCostAsync();

        Assert.Equal(2, report.Items.Count);

        var burgerLine = report.Items.Single(l => l.ItemCode == "MENU-1");
        Assert.True(burgerLine.HasRecipe);
        Assert.Equal(4, burgerLine.RecipeCost); // 2 units of bread at 2 each
        Assert.Equal(46, burgerLine.GrossProfit); // 50 - 4

        var waterLine = report.Items.Single(l => l.ItemCode == "MENU-2");
        Assert.False(waterLine.HasRecipe);
        Assert.Equal(3, waterLine.RecipeCost); // falls back to its own AverageCost
        Assert.Equal(7, waterLine.GrossProfit); // 10 - 3
    }

    [Fact]
    public async Task GetWasteAnalysis_AggregatesByItemAndReasonAndComputesPercentOfCogs()
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
        var item = new Item { Code = "ITM-A", NameAr = "صنف أ", NameEn = "Item A", UnitOfMeasure = "قطعة", ItemCategory = category, QuantityOnHand = 100, AverageCost = 8 };
        var warehouse = new Warehouse { Code = "WH-MAIN", NameAr = "المخزن الرئيسي", NameEn = "Main Warehouse" };
        ctx.ItemCategories.Add(category);
        ctx.Items.Add(item);
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        var wasteService = new WasteEntryService(ctx, new InventoryService(ctx, new ItemLotService(ctx)));
        await wasteService.CreateAsync(new CreateWasteEntryDto
        {
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            FiscalPeriodId = period.Id,
            WasteDate = new DateTime(2026, 8, 10),
            Quantity = 5,
            Reason = (int)WasteReason.Waste
        }); // cost = 5 * 8 = 40
        await wasteService.CreateAsync(new CreateWasteEntryDto
        {
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            FiscalPeriodId = period.Id,
            WasteDate = new DateTime(2026, 8, 12),
            Quantity = 2,
            Reason = (int)WasteReason.Expired
        }); // cost = 2 * 8 = 16

        // A separate, non-waste stock issue in the same period, so COGS isn't only the waste itself.
        ctx.StockMovements.Add(new StockMovement
        {
            MovementNumber = "SM-999999",
            MovementDate = new DateTime(2026, 8, 11),
            MovementType = StockMovementType.Issue,
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            Quantity = 5.5m,
            UnitCost = 8,
            TotalCost = 44
        });
        await ctx.SaveChangesAsync();

        var service = new InventoryReportService(ctx);
        var report = await service.GetWasteAnalysisAsync(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        Assert.Equal(56, report.TotalWasteCost); // 40 + 16
        Assert.Equal(7, report.TotalWasteQuantity); // 5 + 2
        Assert.Equal(100, report.CogsInPeriod); // 40 + 16 + 44
        Assert.Equal(56, report.WasteCostPercentOfCogs); // 56 / 100 * 100

        var itemLine = Assert.Single(report.TopWastedItems);
        Assert.Equal("ITM-A", itemLine.ItemCode);
        Assert.Equal(56, itemLine.TotalCost);
        Assert.Equal(2, itemLine.EntryCount);

        Assert.Equal(2, report.ByReason.Count);
        var wasteReasonLine = report.ByReason.Single(l => l.Reason == WasteReason.Waste);
        Assert.Equal(40, wasteReasonLine.TotalCost);
        var expiredReasonLine = report.ByReason.Single(l => l.Reason == WasteReason.Expired);
        Assert.Equal(16, expiredReasonLine.TotalCost);

        Assert.NotEmpty(report.WeeklyTrend);
        Assert.Equal(56, report.WeeklyTrend.Sum(p => p.TotalCost));
    }
}
