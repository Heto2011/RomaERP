using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
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
}
