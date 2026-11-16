using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ManufacturingServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private record SeedResult(ApplicationDbContext Ctx, Warehouse Warehouse, Item Tomatoes, Item Spices, Item Sauce);

    private static async Task<SeedResult> SeedAsync()
    {
        var ctx = CreateContext();
        var warehouse = new Warehouse { Code = "WH-1", NameAr = "المطبخ", NameEn = "Kitchen" };
        var category = new ItemCategory { Code = "CAT-1", NameAr = "تصنيف", NameEn = "Category" };

        var tomatoes = new Item { Code = "RM-TOM", NameAr = "طماطم", NameEn = "Tomatoes", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, QuantityOnHand = 100, AverageCost = 10 };
        var spices = new Item { Code = "RM-SPICE", NameAr = "بهارات", NameEn = "Spices", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, QuantityOnHand = 20, AverageCost = 40 };
        var sauce = new Item { Code = "SEMI-SAUCE", NameAr = "صلصة طماطم", NameEn = "Tomato Sauce", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, QuantityOnHand = 0, AverageCost = 0 };

        ctx.Warehouses.Add(warehouse);
        ctx.ItemCategories.Add(category);
        ctx.Items.AddRange(tomatoes, spices, sauce);
        await ctx.SaveChangesAsync();

        return new SeedResult(ctx, warehouse, tomatoes, spices, sauce);
    }

    [Fact]
    public async Task SetBomAsync_CreatesBomWithLines()
    {
        var seed = await SeedAsync();
        var service = new ManufacturingService(seed.Ctx, new ItemLotService(seed.Ctx));

        var bom = await service.SetBomAsync(seed.Sauce.Id, new SetManufacturingBomDto
        {
            OutputQuantity = 20,
            Lines = new List<CreateManufacturingBomLineDto>
            {
                new() { RawMaterialItemId = seed.Tomatoes.Id, QuantityPerBatch = 18 },
                new() { RawMaterialItemId = seed.Spices.Id, QuantityPerBatch = 2 },
            }
        });

        Assert.Equal(20, bom.OutputQuantity);
        Assert.Equal(2, bom.Lines.Count);
    }

    [Fact]
    public async Task SetBomAsync_RejectsOutputItemAsItsOwnIngredient()
    {
        var seed = await SeedAsync();
        var service = new ManufacturingService(seed.Ctx, new ItemLotService(seed.Ctx));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SetBomAsync(seed.Sauce.Id, new SetManufacturingBomDto
        {
            OutputQuantity = 20,
            Lines = new List<CreateManufacturingBomLineDto> { new() { RawMaterialItemId = seed.Sauce.Id, QuantityPerBatch = 1 } }
        }));
    }

    [Fact]
    public async Task CreateOrderAsync_ScalesConsumptionAndProducesOutputAtRolledUpCost()
    {
        var seed = await SeedAsync();
        var service = new ManufacturingService(seed.Ctx, new ItemLotService(seed.Ctx));

        await service.SetBomAsync(seed.Sauce.Id, new SetManufacturingBomDto
        {
            OutputQuantity = 20,
            Lines = new List<CreateManufacturingBomLineDto>
            {
                new() { RawMaterialItemId = seed.Tomatoes.Id, QuantityPerBatch = 18 },
                new() { RawMaterialItemId = seed.Spices.Id, QuantityPerBatch = 2 },
            }
        });

        // Half a batch: 10kg of sauce instead of the full 20kg yield.
        var order = await service.CreateOrderAsync(new CreateManufacturingOrderDto
        {
            OutputItemId = seed.Sauce.Id,
            WarehouseId = seed.Warehouse.Id,
            ProductionDate = DateTime.UtcNow.Date,
            ProducedQuantity = 10,
        });

        // Consumption scales by 10/20 = 0.5: 9kg tomatoes, 1kg spices.
        Assert.Equal(91, seed.Tomatoes.QuantityOnHand); // 100 - 9
        Assert.Equal(19, seed.Spices.QuantityOnHand); // 20 - 1

        // Cost: 9*10 + 1*40 = 130, all rolled into the 10kg produced.
        Assert.Equal(130m, order.TotalCost);
        Assert.Equal(10, seed.Sauce.QuantityOnHand);
        Assert.Equal(13m, seed.Sauce.AverageCost); // 130 / 10

        var movements = await seed.Ctx.StockMovements.ToListAsync();
        Assert.Equal(3, movements.Count); // 2 issues + 1 receipt
        Assert.All(movements, m => Assert.Null(m.JournalEntryId)); // internal control only, no GL posting
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsWhenRawMaterialStockInsufficient()
    {
        var seed = await SeedAsync();
        var service = new ManufacturingService(seed.Ctx, new ItemLotService(seed.Ctx));

        await service.SetBomAsync(seed.Sauce.Id, new SetManufacturingBomDto
        {
            OutputQuantity = 20,
            Lines = new List<CreateManufacturingBomLineDto>
            {
                new() { RawMaterialItemId = seed.Tomatoes.Id, QuantityPerBatch = 1000 }, // far more than on hand
            }
        });

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateOrderAsync(new CreateManufacturingOrderDto
        {
            OutputItemId = seed.Sauce.Id,
            WarehouseId = seed.Warehouse.Id,
            ProductionDate = DateTime.UtcNow.Date,
            ProducedQuantity = 20,
        }));
    }

    private static async Task<(SeedResult Seed, ManufacturingService Svc)> WithOverheadAsync(decimal conversionPerBatch, decimal monthlyFixed, decimal normalBatches)
    {
        var seed = await SeedAsync();
        seed.Ctx.Accounts.AddRange(
            new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit },
            new Account { Code = "5510", NameAr = "تكاليف محمّلة", NameEn = "Capitalised", AccountType = AccountType.Expense, Nature = AccountNature.Credit });
        var year = new FiscalYear { Name = "Y", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        seed.Ctx.FiscalYears.Add(year);
        seed.Ctx.FiscalPeriods.Add(new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Nov", PeriodNumber = 11, StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) });
        await seed.Ctx.SaveChangesAsync();
        var svc = new ManufacturingService(seed.Ctx, new ItemLotService(seed.Ctx));
        await svc.SetBomAsync(seed.Sauce.Id, new SetManufacturingBomDto
        {
            OutputQuantity = 20, ConversionCostPerBatch = conversionPerBatch,
            Lines = { new CreateManufacturingBomLineDto { RawMaterialItemId = seed.Tomatoes.Id, QuantityPerBatch = 18 }, new CreateManufacturingBomLineDto { RawMaterialItemId = seed.Spices.Id, QuantityPerBatch = 2 } }
        });
        await svc.SetSiteAsync(seed.Warehouse.Id, new SetManufacturingSiteDto { MonthlyFixedOverhead = monthlyFixed, NormalMonthlyBatches = normalBatches });
        return (seed, svc);
    }

    private static CreateManufacturingOrderDto Order(SeedResult seed, decimal qty = 20) => new() { OutputItemId = seed.Sauce.Id, WarehouseId = seed.Warehouse.Id, ProductionDate = new DateTime(2026, 11, 10), ProducedQuantity = qty };

    [Fact]
    public async Task Conversion_AndFixedOverheadOnNormalCapacity_GoIntoTheCostOfWhatIsProduced()
    {
        var (seed, svc) = await WithOverheadAsync(conversionPerBatch: 40, monthlyFixed: 1000, normalBatches: 10);
        var order = await svc.CreateOrderAsync(Order(seed));
        Assert.Equal(260m, order.MaterialCost);
        Assert.Equal(40m, order.ConversionCost);
        Assert.Equal(100m, order.FixedOverheadAllocated);       // 1000 / 10 normal batches x 1 batch
        Assert.Equal(400m, order.TotalCost);
        Assert.Equal(20m, (await seed.Ctx.Items.SingleAsync(i => i.Id == seed.Sauce.Id)).AverageCost);
        var lines = await seed.Ctx.JournalEntryLines.Include(l => l.Account).ToListAsync();
        Assert.Contains(lines, l => l.Account!.Code == "1160" && l.Debit == 140m);
        Assert.Contains(lines, l => l.Account!.Code == "5510" && l.Credit == 140m);
    }

    [Fact]
    public async Task WhenTheSiteWorksBelowNormal_OnlyTheNormalRateIsAbsorbed_TheRestStaysAnExpense()
    {
        var (seed, svc) = await WithOverheadAsync(0, 1000, 10);
        await svc.CreateOrderAsync(Order(seed, 40));            // 2 batches of a normal 10
        var row = (await svc.GetOverheadReportAsync(2026, 11)).Single();
        Assert.Equal(200m, row.Absorbed);
        Assert.Equal(800m, row.Unabsorbed);                      // not put on the stock cost
        Assert.Equal(20m, row.CapacityUsedPercent);
    }

    [Fact]
    public async Task WhenTheSiteWorksAboveNormal_AbsorptionNeverExceedsTheRealMonthlyOverhead()
    {
        var (seed, svc) = await WithOverheadAsync(0, 1000, 2);
        for (var i = 0; i < 3; i++) await svc.CreateOrderAsync(Order(seed));
        var orders = await seed.Ctx.ManufacturingOrders.OrderBy(o => o.OrderNumber).ToListAsync();
        Assert.Equal(new[] { 500m, 500m, 0m }, orders.Select(o => o.FixedOverheadAllocated).ToArray());
        Assert.Equal(1000m, (await svc.GetOverheadReportAsync(2026, 11)).Single().Absorbed);
    }

    [Fact]
    public async Task WithoutASite_OrNoConversionCost_NothingExtraIsPosted()
    {
        var seed = await SeedAsync();
        var svc = new ManufacturingService(seed.Ctx, new ItemLotService(seed.Ctx));
        await svc.SetBomAsync(seed.Sauce.Id, new SetManufacturingBomDto { OutputQuantity = 20, Lines = { new CreateManufacturingBomLineDto { RawMaterialItemId = seed.Tomatoes.Id, QuantityPerBatch = 18 } } });
        var order = await svc.CreateOrderAsync(new CreateManufacturingOrderDto { OutputItemId = seed.Sauce.Id, WarehouseId = seed.Warehouse.Id, ProductionDate = DateTime.UtcNow.Date, ProducedQuantity = 20 });
        Assert.Equal(180m, order.TotalCost);
        Assert.Empty(seed.Ctx.JournalEntries);
    }

    [Fact]
    public async Task FixedOverheadNeedsANormalCapacity()
    {
        var seed = await SeedAsync();
        var svc = new ManufacturingService(seed.Ctx, new ItemLotService(seed.Ctx));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.SetSiteAsync(seed.Warehouse.Id, new SetManufacturingSiteDto { MonthlyFixedOverhead = 500, NormalMonthlyBatches = 0 }));
    }
}
