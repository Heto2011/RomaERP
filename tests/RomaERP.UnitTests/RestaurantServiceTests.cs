using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Restaurant;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class RestaurantServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private record SeedResult(
        ApplicationDbContext Ctx, Account Cash, Account Revenue, Account OutputVat, Account Cogs, Account Inventory,
        Warehouse Warehouse, FiscalPeriod Period, RestaurantTable Table, Item Flour, Item Pizza, Item Water);

    private static async Task<SeedResult> SeedAsync(decimal vatRate = 0.14m)
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var outputVat = new Account { Code = "2161", NameAr = "ضريبة مخرجات", NameEn = "Output VAT", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var cogs = new Account { Code = "5500", NameAr = "تكلفة البضاعة المباعة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var inventory = new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };

        var warehouse = new Warehouse { Code = "WH-1", NameAr = "مخزن المطبخ", NameEn = "Kitchen Warehouse" };
        var category = new ItemCategory { Code = "CAT-1", NameAr = "تصنيف", NameEn = "Category" };
        var table = new RestaurantTable { Number = "T1", SectionName = "الصالة", Capacity = 4 };

        var flour = new Item { Code = "RM-FLOUR", NameAr = "دقيق", NameEn = "Flour", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, QuantityOnHand = 100, AverageCost = 20 };
        var pizza = new Item { Code = "MENU-PIZZA", NameAr = "بيتزا", NameEn = "Pizza", UnitOfMeasure = "قطعة", ItemCategoryId = category.Id, IsMenuItem = true, MenuPrice = 100 };
        var water = new Item { Code = "MENU-WATER", NameAr = "مياه معدنية", NameEn = "Water", UnitOfMeasure = "قطعة", ItemCategoryId = category.Id, IsMenuItem = true, MenuPrice = 10, QuantityOnHand = 50, AverageCost = 5 };

        ctx.Accounts.AddRange(cash, revenue, outputVat, cogs, inventory);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        ctx.Warehouses.Add(warehouse);
        ctx.ItemCategories.Add(category);
        ctx.RestaurantTables.Add(table);
        ctx.Items.AddRange(flour, pizza, water);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = vatRate, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        ctx.MenuRecipeLines.Add(new MenuRecipeLine { MenuItemId = pizza.Id, RawMaterialItemId = flour.Id, QuantityPerUnit = 0.5m });
        await ctx.SaveChangesAsync();

        return new SeedResult(ctx, cash, revenue, outputVat, cogs, inventory, warehouse, period, table, flour, pizza, water);
    }

    private static RestaurantService BuildService(ApplicationDbContext ctx)
        => new(ctx, new SalesService(ctx, new FakeHtmlToPdfRenderer()));

    [Fact]
    public async Task CreateOrder_DineIn_OccupiesTableAndGeneratesOrderNumber()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto
        {
            OrderType = RestaurantOrderType.DineIn,
            TableId = seed.Table.Id,
            WarehouseId = seed.Warehouse.Id
        });

        Assert.StartsWith("RO-", order.OrderNumber);
        Assert.Equal(RestaurantOrderStatus.Open, order.Status);

        var table = await seed.Ctx.RestaurantTables.FirstAsync(t => t.Id == seed.Table.Id);
        Assert.Equal(RestaurantTableStatus.Occupied, table.Status);
    }

    [Fact]
    public async Task CreateOrder_DineIn_TableAlreadyOccupied_Throws()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.DineIn, TableId = seed.Table.Id, WarehouseId = seed.Warehouse.Id });

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateOrderAsync(new CreateRestaurantOrderDto
        {
            OrderType = RestaurantOrderType.DineIn,
            TableId = seed.Table.Id,
            WarehouseId = seed.Warehouse.Id
        }));
    }

    [Fact]
    public async Task CreateOrder_TakeawayWithTableId_Throws()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateOrderAsync(new CreateRestaurantOrderDto
        {
            OrderType = RestaurantOrderType.Takeaway,
            TableId = seed.Table.Id,
            WarehouseId = seed.Warehouse.Id
        }));
    }

    [Fact]
    public async Task BillOrder_RecipeItem_ConsumesRawMaterialAndPostsSeparateCogsEntry()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.DineIn, TableId = seed.Table.Id, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Pizza.Id, Quantity = 2 });

        var billed = await service.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });

        Assert.Equal(RestaurantOrderStatus.Billed, billed.Status);
        Assert.Equal(200, billed.SubTotal);
        Assert.Equal(28, billed.VatAmount);
        Assert.Equal(228, billed.TotalAmount);
        Assert.NotNull(billed.SalesInvoiceId);

        // Revenue side: posted by ISalesService as a plain (non-item-linked) line.
        var invoiceEntry = await seed.Ctx.JournalEntries.Include(e => e.Lines)
            .Where(e => e.Reference == "SALES-INVOICE").SingleAsync();
        Assert.Contains(invoiceEntry.Lines, l => l.AccountId == seed.Cash.Id && l.Debit == 228);
        Assert.Contains(invoiceEntry.Lines, l => l.AccountId == seed.Revenue.Id && l.Credit == 200);
        Assert.Contains(invoiceEntry.Lines, l => l.AccountId == seed.OutputVat.Id && l.Credit == 28);

        // Recipe COGS side: 2 pizzas * 0.5kg flour * 20 cost/kg = 20, posted separately by RestaurantService.
        var cogsEntry = await seed.Ctx.JournalEntries.Include(e => e.Lines)
            .Where(e => e.Reference == "RESTAURANT-ORDER").SingleAsync();
        Assert.Contains(cogsEntry.Lines, l => l.AccountId == seed.Cogs.Id && l.Debit == 20);
        Assert.Contains(cogsEntry.Lines, l => l.AccountId == seed.Inventory.Id && l.Credit == 20);

        var flour = await seed.Ctx.Items.FirstAsync(i => i.Id == seed.Flour.Id);
        Assert.Equal(99, flour.QuantityOnHand); // 100 - (2 * 0.5)

        var pizza = await seed.Ctx.Items.FirstAsync(i => i.Id == seed.Pizza.Id);
        Assert.Equal(0, pizza.QuantityOnHand); // the finished product itself was never decremented

        var table = await seed.Ctx.RestaurantTables.FirstAsync(t => t.Id == seed.Table.Id);
        Assert.Equal(RestaurantTableStatus.Available, table.Status);
    }

    [Fact]
    public async Task BillOrder_NonRecipeItem_ReliesOnSalesServiceItemLineCogsOnly()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 3 });

        await service.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });

        // No RestaurantService-authored COGS entry should exist (no recipe items in this order).
        Assert.False(await seed.Ctx.JournalEntries.AnyAsync(e => e.Reference == "RESTAURANT-ORDER"));

        // SalesService posted its own COGS entry for the item-linked line: 3 * 5 = 15.
        var cogsEntry = await seed.Ctx.JournalEntries.Include(e => e.Lines)
            .Where(e => e.Reference == "SALES-INVOICE" && e.Lines.Any(l => l.AccountId == seed.Cogs.Id))
            .SingleAsync();
        Assert.Contains(cogsEntry.Lines, l => l.AccountId == seed.Cogs.Id && l.Debit == 15);

        var water = await seed.Ctx.Items.FirstAsync(i => i.Id == seed.Water.Id);
        Assert.Equal(47, water.QuantityOnHand);
    }

    [Fact]
    public async Task BillOrder_InsufficientRawMaterialStock_ThrowsWithoutCreatingInvoice()
    {
        var seed = await SeedAsync();
        seed.Flour.QuantityOnHand = 0.4m; // less than the 0.5kg one pizza needs
        await seed.Ctx.SaveChangesAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.DineIn, TableId = seed.Table.Id, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Pizza.Id, Quantity = 1 });

        await Assert.ThrowsAsync<ValidationAppException>(() => service.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id }));

        Assert.Empty(await seed.Ctx.SalesInvoices.ToListAsync());
    }

    [Fact]
    public async Task SetLineDiscount_ReducesOrderSubTotal_AndRejectsExcessiveAmount()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        var afterAdd = await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 2 }); // LineTotal = 20
        var lineId = afterAdd.Lines.Single().Id;

        var discounted = await service.SetLineDiscountAsync(order.Id, lineId, new SetLineDiscountDto { DiscountAmount = 5 });
        Assert.Equal(5, discounted.Lines.Single().DiscountAmount);
        Assert.Equal(5, discounted.TotalDiscount);
        Assert.Equal(15, discounted.SubTotal); // 20 - 5

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.SetLineDiscountAsync(order.Id, lineId, new SetLineDiscountDto { DiscountAmount = 25 }));
    }

    [Fact]
    public async Task SetOrderDiscount_AppliedProportionallyAtBilling_ReducesInvoiceRevenue()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 2 }); // LineTotal = 20

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.SetOrderDiscountAsync(order.Id, new SetOrderDiscountDto { DiscountAmount = 100 }));

        var discounted = await service.SetOrderDiscountAsync(order.Id, new SetOrderDiscountDto { DiscountAmount = 4 });
        Assert.Equal(16, discounted.SubTotal); // 20 - 4

        var billed = await service.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });

        var invoice = await seed.Ctx.SalesInvoices.FirstAsync(i => i.Id == billed.SalesInvoiceId);
        Assert.Equal(16, invoice.SubTotal); // one line: unit price drops from 10 to 8, so 8 * 2 = 16 exactly
    }

    [Fact]
    public async Task CancelOrder_FreesTableWithoutPostingAnything()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.DineIn, TableId = seed.Table.Id, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Pizza.Id, Quantity = 1 });

        var cancelled = await service.CancelOrderAsync(order.Id);

        Assert.Equal(RestaurantOrderStatus.Cancelled, cancelled.Status);
        var table = await seed.Ctx.RestaurantTables.FirstAsync(t => t.Id == seed.Table.Id);
        Assert.Equal(RestaurantTableStatus.Available, table.Status);
        Assert.Empty(await seed.Ctx.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task UpdateLineQuantity_ToZero_RemovesLine()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        var withLine = await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 2 });
        var lineId = withLine.Lines.Single().Id;

        var withoutLine = await service.UpdateLineQuantityAsync(order.Id, lineId, new UpdateOrderLineQuantityDto { Quantity = 0 });

        Assert.Empty(withoutLine.Lines);
    }
}
