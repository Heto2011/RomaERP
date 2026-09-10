using System.Text;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Restaurant;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class DeliveryReconciliationServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Stream CsvStream(string csv) => new MemoryStream(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task ImportAsync_ParsesCsvAndSummarizesTotal()
    {
        var ctx = CreateContext();
        var service = new DeliveryReconciliationService(ctx);
        var csv = "Date,Description,Amount\n2026-08-10,Payout 1,1200.50\n2026-08-17,Payout 2,980.00\n";

        var result = await service.ImportAsync(CsvStream(csv), "talabat-august.csv", "طلبات", "user-1");

        Assert.Equal("طلبات", result.PlatformName);
        Assert.Equal(2, result.LineCount);
        Assert.Equal(2180.50m, result.TotalAmount);
        Assert.Equal(new DateTime(2026, 8, 10), result.PeriodFrom);
        Assert.Equal(new DateTime(2026, 8, 17), result.PeriodTo);
    }

    [Fact]
    public async Task ImportAsync_RejectsBlankPlatformName()
    {
        var ctx = CreateContext();
        var service = new DeliveryReconciliationService(ctx);
        var csv = "2026-08-10,Payout,100\n";

        await Assert.ThrowsAsync<ValidationAppException>(() => service.ImportAsync(CsvStream(csv), "x.csv", "  ", "user-1"));
    }

    [Fact]
    public async Task GetReconciliation_ComparesRealDeliveryRevenueAgainstImportedSettlements()
    {
        var ctx = CreateContext();
        var category = new ItemCategory { NameAr = "عام", NameEn = "General" };
        var burger = new Item { Code = "MENU-1", NameAr = "برجر", NameEn = "Burger", UnitOfMeasure = "قطعة", ItemCategory = category, IsMenuItem = true, MenuPrice = 50 };
        var warehouse = new Warehouse { Code = "WH-MAIN", NameAr = "المخزن الرئيسي", NameEn = "Main Warehouse" };
        ctx.ItemCategories.Add(category);
        ctx.Items.Add(burger);
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        // Real delivery revenue in RomaERP: 40 + 40 = 80 for August.
        var billedOrder = new RestaurantOrder
        {
            OrderNumber = "RO-1",
            OrderType = RestaurantOrderType.Delivery,
            OrderDate = new DateTime(2026, 8, 10),
            WarehouseId = warehouse.Id,
            Status = RestaurantOrderStatus.Billed,
            Lines = new List<RestaurantOrderLine>
            {
                new() { LineNumber = 1, ItemId = burger.Id, Quantity = 2, UnitPrice = 40, LineTotal = 80 }
            }
        };
        // Excluded: not billed yet.
        var openOrder = new RestaurantOrder
        {
            OrderNumber = "RO-2",
            OrderType = RestaurantOrderType.Delivery,
            OrderDate = new DateTime(2026, 8, 12),
            WarehouseId = warehouse.Id,
            Status = RestaurantOrderStatus.Open,
            Lines = new List<RestaurantOrderLine>
            {
                new() { LineNumber = 1, ItemId = burger.Id, Quantity = 1, UnitPrice = 999, LineTotal = 999 }
            }
        };
        // Excluded: dine-in, not delivery.
        var dineInOrder = new RestaurantOrder
        {
            OrderNumber = "RO-3",
            OrderType = RestaurantOrderType.DineIn,
            OrderDate = new DateTime(2026, 8, 12),
            WarehouseId = warehouse.Id,
            Status = RestaurantOrderStatus.Billed,
            Lines = new List<RestaurantOrderLine>
            {
                new() { LineNumber = 1, ItemId = burger.Id, Quantity = 1, UnitPrice = 500, LineTotal = 500 }
            }
        };
        ctx.RestaurantOrders.AddRange(billedOrder, openOrder, dineInOrder);
        await ctx.SaveChangesAsync();

        var service = new DeliveryReconciliationService(ctx);
        await service.ImportAsync(CsvStream("2026-08-15,Settlement,75\n"), "s.csv", "طلبات", "user-1");

        var report = await service.GetReconciliationAsync(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        Assert.Equal(80, report.ExpectedRevenue);
        Assert.Equal(75, report.ReceivedAmount);
        Assert.Equal(-5, report.Variance);
    }
}
