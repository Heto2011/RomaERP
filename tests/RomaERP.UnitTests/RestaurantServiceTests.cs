using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Inventory.Services;
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

    internal record SeedResult(
        ApplicationDbContext Ctx, Account Cash, Account Revenue, Account OutputVat, Account Cogs, Account Inventory, Account Ar,
        Warehouse Warehouse, FiscalPeriod Period, RestaurantTable Table, Item Flour, Item Pizza, Item Water);

    internal static async Task<SeedResult> SeedAsync(decimal vatRate = 0.14m)
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var outputVat = new Account { Code = "2161", NameAr = "ضريبة مخرجات", NameEn = "Output VAT", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var cogs = new Account { Code = "5500", NameAr = "تكلفة البضاعة المباعة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var inventory = new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var ar = new Account { Code = "1120", NameAr = "العملاء", NameEn = "AR", AccountType = AccountType.Asset, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };

        var warehouse = new Warehouse { Code = "WH-1", NameAr = "مخزن المطبخ", NameEn = "Kitchen Warehouse" };
        var category = new ItemCategory { Code = "CAT-1", NameAr = "تصنيف", NameEn = "Category" };
        var table = new RestaurantTable { Number = "T1", SectionName = "الصالة", Capacity = 4 };

        var flour = new Item { Code = "RM-FLOUR", NameAr = "دقيق", NameEn = "Flour", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, QuantityOnHand = 100, AverageCost = 20 };
        var pizza = new Item { Code = "MENU-PIZZA", NameAr = "بيتزا", NameEn = "Pizza", UnitOfMeasure = "قطعة", ItemCategoryId = category.Id, IsMenuItem = true, MenuPrice = 100 };
        var water = new Item { Code = "MENU-WATER", NameAr = "مياه معدنية", NameEn = "Water", UnitOfMeasure = "قطعة", ItemCategoryId = category.Id, IsMenuItem = true, MenuPrice = 10, QuantityOnHand = 50, AverageCost = 5 };

        ctx.Accounts.AddRange(cash, revenue, outputVat, cogs, inventory, ar);
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

        return new SeedResult(ctx, cash, revenue, outputVat, cogs, inventory, ar, warehouse, period, table, flour, pizza, water);
    }

    internal static RestaurantService BuildService(ApplicationDbContext ctx)
        => new(ctx, new SalesService(ctx, new FakeHtmlToPdfRenderer(), new ExchangeRateService(ctx, new FakeExchangeRateProvider())), new ItemLotService(ctx), new JournalEntryService(ctx));

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

    [Fact]
    public async Task VoidOrder_RecipeItemCashOrder_ReversesRevenueVatCogsAndInventory()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.DineIn, TableId = seed.Table.Id, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Pizza.Id, Quantity = 2 });
        var billed = await service.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });

        var voided = await service.VoidOrderAsync(order.Id, new VoidOrderDto { Reason = "طلب غلط" });

        Assert.Equal(RestaurantOrderStatus.Voided, voided.Status);
        Assert.Equal("طلب غلط", voided.VoidReason);
        Assert.NotNull(voided.VoidedAtUtc);

        // Both original entries (revenue+VAT+cash, and recipe COGS) are now Reversed, with mirror entries posted.
        var allEntries = await seed.Ctx.JournalEntries.Include(e => e.Lines).ToListAsync();
        Assert.Equal(4, allEntries.Count); // original revenue + original COGS + 2 reversals
        Assert.Equal(2, allEntries.Count(e => e.Status == JournalEntryStatus.Reversed));

        // The reversal entry's own Reference is set to the *original* entry's number (per
        // JournalEntryService.ReverseAsync), not "SALES-INVOICE"/"RESTAURANT-ORDER" — so it's identified
        // by its Status and lines instead.
        var reversalOfRevenue = allEntries.Single(e => e.Status == JournalEntryStatus.Posted && e.Lines.Any(l => l.AccountId == seed.Cash.Id && l.Credit == 228));
        Assert.Contains(reversalOfRevenue.Lines, l => l.AccountId == seed.Revenue.Id && l.Debit == 200);
        Assert.Contains(reversalOfRevenue.Lines, l => l.AccountId == seed.OutputVat.Id && l.Debit == 28);

        var reversalOfCogs = allEntries.Single(e => e.Status == JournalEntryStatus.Posted && e.Lines.Any(l => l.AccountId == seed.Inventory.Id && l.Debit == 20));
        Assert.Contains(reversalOfCogs.Lines, l => l.AccountId == seed.Cogs.Id && l.Credit == 20);

        var flour = await seed.Ctx.Items.FirstAsync(i => i.Id == seed.Flour.Id);
        Assert.Equal(100, flour.QuantityOnHand); // fully restored: 99 + 1 (2 pizzas * 0.5kg)
    }

    [Fact]
    public async Task VoidOrder_NonRecipeItem_RestoresStockIssuedBySalesService()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 3 });
        await service.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });

        await service.VoidOrderAsync(order.Id, new VoidOrderDto { Reason = "استبدال" });

        var water = await seed.Ctx.Items.FirstAsync(i => i.Id == seed.Water.Id);
        Assert.Equal(50, water.QuantityOnHand); // fully restored: 47 + 3
    }

    [Fact]
    public async Task VoidOrder_RejectsAnOrderThatWasNeverBilled()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });

        await Assert.ThrowsAsync<ValidationAppException>(() => service.VoidOrderAsync(order.Id, new VoidOrderDto { Reason = "سبب" }));
    }

    [Fact]
    public async Task VoidOrder_CreditDeliveryOrder_ReducesPlatformArBalance()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Delivery, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 1 });
        await service.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Credit, FiscalPeriodId = seed.Period.Id, DeliveryPlatformName = "Talabat" });

        var platformCustomer = await seed.Ctx.Customers.SingleAsync(c => c.NameAr == "Talabat");
        Assert.True(platformCustomer.ArBalance > 0);

        await service.VoidOrderAsync(order.Id, new VoidOrderDto { Reason = "إلغاء طلب دليفري" });

        var afterVoid = await seed.Ctx.Customers.SingleAsync(c => c.Id == platformCustomer.Id);
        Assert.Equal(0, afterVoid.ArBalance);
    }

    [Fact]
    public async Task AddLine_DefaultsKitchenStatusToPending_AndCanBeAdvanced()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        var withLine = await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 1 });
        var lineId = withLine.Lines.Single().Id;
        Assert.Equal(KitchenLineStatus.Pending, withLine.Lines.Single().KitchenStatus);

        var preparing = await service.SetLineKitchenStatusAsync(order.Id, lineId, new SetLineKitchenStatusDto { Status = KitchenLineStatus.Preparing });
        Assert.Equal(KitchenLineStatus.Preparing, preparing.Lines.Single().KitchenStatus);
    }

    [Fact]
    public async Task SplitOrder_MovesSelectedLinesToNewOpenOrder_KeepingTheOriginalOpen()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.DineIn, TableId = seed.Table.Id, WarehouseId = seed.Warehouse.Id });
        var afterPizza = await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Pizza.Id, Quantity = 1 });
        var afterWater = await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 1 });
        var waterLineId = afterWater.Lines.Single(l => l.ItemId == seed.Water.Id).Id;

        var result = await service.SplitOrderAsync(order.Id, new SplitOrderDto { LineIds = new List<Guid> { waterLineId } });

        Assert.Equal(RestaurantOrderStatus.Open, result.OriginalOrder.Status);
        Assert.Single(result.OriginalOrder.Lines);
        Assert.Equal(seed.Pizza.Id, result.OriginalOrder.Lines.Single().ItemId);

        Assert.Equal(RestaurantOrderStatus.Open, result.NewOrder.Status);
        Assert.Single(result.NewOrder.Lines);
        Assert.Equal(seed.Water.Id, result.NewOrder.Lines.Single().ItemId);
        Assert.Equal(order.TableId, result.NewOrder.TableId);
        Assert.NotEqual(order.Id, result.NewOrder.Id);

        // Both halves can be billed independently.
        var billedOriginal = await service.BillOrderAsync(result.OriginalOrder.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });
        var billedNew = await service.BillOrderAsync(result.NewOrder.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });
        Assert.Equal(RestaurantOrderStatus.Billed, billedOriginal.Status);
        Assert.Equal(RestaurantOrderStatus.Billed, billedNew.Status);
    }

    [Fact]
    public async Task SplitOrder_RejectsMovingAllLines_AndRejectsUnknownLine()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        var withLine = await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 1 });
        var lineId = withLine.Lines.Single().Id;

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.SplitOrderAsync(order.Id, new SplitOrderDto { LineIds = new List<Guid> { lineId } }));

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.SplitOrderAsync(order.Id, new SplitOrderDto { LineIds = new List<Guid> { Guid.NewGuid() } }));
    }

    [Fact]
    public async Task VoidOrder_CreditOrderAlreadyPartiallyPaid_Rejected()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Delivery, WarehouseId = seed.Warehouse.Id });
        await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 1 });
        var billed = await service.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Credit, FiscalPeriodId = seed.Period.Id, DeliveryPlatformName = "Talabat" });

        var invoice = await seed.Ctx.SalesInvoices.FirstAsync(i => i.Id == billed.SalesInvoiceId);
        invoice.PaidAmount = 1;
        await seed.Ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationAppException>(() => service.VoidOrderAsync(order.Id, new VoidOrderDto { Reason = "سبب" }));
    }

    // ---------- Table move / merge ----------

    private static async Task<RestaurantTable> AddTableAsync(ApplicationDbContext ctx, string number)
    {
        var table = new RestaurantTable { Number = number, SectionName = "الصالة", Capacity = 4 };
        ctx.RestaurantTables.Add(table);
        await ctx.SaveChangesAsync();
        return table;
    }

    private static async Task<RestaurantOrderDto> OpenDineInAsync(RestaurantService service, SeedResult seed, RestaurantTable table, params (Item Item, decimal Qty)[] lines)
    {
        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.DineIn, TableId = table.Id, WarehouseId = seed.Warehouse.Id });
        foreach (var (item, qty) in lines)
            order = await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = item.Id, Quantity = qty });
        return order;
    }

    [Fact]
    public async Task MoveOrderToTable_OccupiesTheNewTable_AndFreesTheOldOne()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);
        var t2 = await AddTableAsync(seed.Ctx, "T2");
        var order = await OpenDineInAsync(service, seed, seed.Table, (seed.Water, 2));

        var moved = await service.MoveOrderToTableAsync(order.Id, new MoveOrderTableDto { TargetTableId = t2.Id });

        Assert.Equal(t2.Id, moved.TableId);
        Assert.Equal(RestaurantTableStatus.Available, (await seed.Ctx.RestaurantTables.AsNoTracking().SingleAsync(t => t.Id == seed.Table.Id)).Status);
        Assert.Equal(RestaurantTableStatus.Occupied, (await seed.Ctx.RestaurantTables.AsNoTracking().SingleAsync(t => t.Id == t2.Id)).Status);
        Assert.Single(moved.Lines);
    }

    [Fact]
    public async Task MoveOrderToTable_RefusesABusyTable_ASameTable_AndTakeaway()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);
        var t2 = await AddTableAsync(seed.Ctx, "T2");
        var first = await OpenDineInAsync(service, seed, seed.Table, (seed.Water, 1));
        await OpenDineInAsync(service, seed, t2, (seed.Water, 1));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.MoveOrderToTableAsync(first.Id, new MoveOrderTableDto { TargetTableId = t2.Id }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.MoveOrderToTableAsync(first.Id, new MoveOrderTableDto { TargetTableId = seed.Table.Id }));

        var takeaway = await service.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        var t3 = await AddTableAsync(seed.Ctx, "T3");
        await Assert.ThrowsAsync<ValidationAppException>(() => service.MoveOrderToTableAsync(takeaway.Id, new MoveOrderTableDto { TargetTableId = t3.Id }));
    }

    [Fact]
    public async Task MergeOrders_MovesEveryLineAndTheDiscount_ClosesTheSource_AndFreesItsTable()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);
        var t2 = await AddTableAsync(seed.Ctx, "T2");
        var target = await OpenDineInAsync(service, seed, seed.Table, (seed.Water, 2));
        var source = await OpenDineInAsync(service, seed, t2, (seed.Water, 1), (seed.Pizza, 1));
        await service.SetOrderDiscountAsync(source.Id, new SetOrderDiscountDto { DiscountAmount = 5 });

        var merged = await service.MergeOrdersAsync(target.Id, new MergeOrdersDto { SourceOrderId = source.Id });

        Assert.Equal(3, merged.Lines.Count);
        Assert.Equal(new[] { 1, 2, 3 }, merged.Lines.Select(l => l.LineNumber).OrderBy(n => n).ToArray());
        Assert.Equal(5m, merged.DiscountAmount);
        Assert.Equal(20m + 10m + 100m, merged.GrossSubTotal);
        var closed = await service.GetOrderAsync(source.Id);
        Assert.Equal(RestaurantOrderStatus.Cancelled, closed.Status);
        Assert.Empty(closed.Lines);
        Assert.Contains(target.OrderNumber, closed.Notes);
        Assert.Equal(RestaurantTableStatus.Available, (await seed.Ctx.RestaurantTables.AsNoTracking().SingleAsync(t => t.Id == t2.Id)).Status);
        Assert.Equal(RestaurantTableStatus.Occupied, (await seed.Ctx.RestaurantTables.AsNoTracking().SingleAsync(t => t.Id == seed.Table.Id)).Status);
    }

    [Fact]
    public async Task MergeOrders_RefusesItselfAClosedOrderAndAnotherWarehouse()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);
        var t2 = await AddTableAsync(seed.Ctx, "T2");
        var a = await OpenDineInAsync(service, seed, seed.Table, (seed.Water, 1));
        var b = await OpenDineInAsync(service, seed, t2, (seed.Water, 1));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.MergeOrdersAsync(a.Id, new MergeOrdersDto { SourceOrderId = a.Id }));

        var otherWarehouse = new Warehouse { Code = "WH-2", NameAr = "مخزن 2", NameEn = "Second" };
        seed.Ctx.Warehouses.Add(otherWarehouse);
        await seed.Ctx.SaveChangesAsync();
        (await seed.Ctx.RestaurantOrders.SingleAsync(o => o.Id == b.Id)).WarehouseId = otherWarehouse.Id;
        await seed.Ctx.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => service.MergeOrdersAsync(a.Id, new MergeOrdersDto { SourceOrderId = b.Id }));

        (await seed.Ctx.RestaurantOrders.SingleAsync(o => o.Id == b.Id)).WarehouseId = seed.Warehouse.Id;
        await seed.Ctx.SaveChangesAsync();
        await service.CancelOrderAsync(b.Id);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.MergeOrdersAsync(a.Id, new MergeOrdersDto { SourceOrderId = b.Id }));
    }

    [Fact]
    public async Task CancellingOneOfTwoChecksOnATable_KeepsTheTableOccupied()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);
        var order = await OpenDineInAsync(service, seed, seed.Table, (seed.Water, 1), (seed.Pizza, 1));
        var split = await service.SplitOrderAsync(order.Id, new SplitOrderDto { LineIds = new List<Guid> { order.Lines[0].Id } });

        await service.CancelOrderAsync(split.NewOrder.Id);
        Assert.Equal(RestaurantTableStatus.Occupied, (await seed.Ctx.RestaurantTables.AsNoTracking().SingleAsync(t => t.Id == seed.Table.Id)).Status);

        await service.CancelOrderAsync(order.Id);
        Assert.Equal(RestaurantTableStatus.Available, (await seed.Ctx.RestaurantTables.AsNoTracking().SingleAsync(t => t.Id == seed.Table.Id)).Status);
    }

    private static async Task EnableLoyaltyAsync(ApplicationDbContext ctx)
    {
        var settings = await ctx.CompanySettings.FirstAsync();
        settings.LoyaltyEnabled = true;
        settings.LoyaltySpendPerPoint = 10;
        settings.LoyaltyPointValue = 0.5m;
        settings.LoyaltyMinRedeemPoints = 20;
        await ctx.SaveChangesAsync();
    }

    private static async Task<RestaurantOrderDto> OpenPizzaOrderAsync(RestaurantService service, SeedResult seed, decimal qty, RestaurantTable? table = null)
    {
        var order = await service.CreateOrderAsync(new CreateRestaurantOrderDto
        {
            OrderType = RestaurantOrderType.DineIn, TableId = (table ?? seed.Table).Id, WarehouseId = seed.Warehouse.Id
        });
        return await service.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = seed.Pizza.Id, Quantity = qty });
    }

    [Fact]
    public async Task Loyalty_EarnsPointsOnBill_RedeemsThemAsDiscount_AndVoidReversesBoth()
    {
        var seed = await SeedAsync();
        await EnableLoyaltyAsync(seed.Ctx);
        var service = BuildService(seed.Ctx);

        // First visit: 2 pizzas = 200 -> 20 points, shown as an estimate while the order is open.
        var first = await OpenPizzaOrderAsync(service, seed, 2);
        var attached = await service.SetOrderLoyaltyAsync(first.Id, new SetOrderLoyaltyDto { Phone = "+20 100 123 4567", Name = "أحمد" });
        Assert.Equal(20, attached.LoyaltyPointsEarned);
        Assert.Equal("01001234567", attached.LoyaltyMemberPhone);
        var billedFirst = await service.BillOrderAsync(first.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });
        Assert.Equal(20, billedFirst.LoyaltyPointsEarned);

        var member = await seed.Ctx.LoyaltyMembers.SingleAsync();
        Assert.Equal(20, member.PointsBalance);
        Assert.Equal(1, member.VisitCount);
        Assert.Equal(200m, member.TotalSpent);

        // Second visit, same customer (matched by phone typed in another format): spend all 20 points = 10 off 100.
        var second = await OpenPizzaOrderAsync(service, seed, 1);
        var withRedeem = await service.SetOrderLoyaltyAsync(second.Id, new SetOrderLoyaltyDto { Phone = "01001234567", PointsToRedeem = 20 });
        Assert.Equal(10m, withRedeem.LoyaltyDiscountAmount);
        Assert.Equal(90m, withRedeem.SubTotal);

        var billedSecond = await service.BillOrderAsync(second.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });
        Assert.Equal(9, billedSecond.LoyaltyPointsEarned);
        Assert.Equal(102.6m, billedSecond.TotalAmount);

        member = await seed.Ctx.LoyaltyMembers.SingleAsync();
        Assert.Equal(9, member.PointsBalance); // 20 - 20 + 9
        Assert.Equal(2, member.VisitCount);

        // Voiding the second order gives the redeemed points back and takes the earned ones away.
        await service.VoidOrderAsync(second.Id, new VoidOrderDto { Reason = "اختبار" });
        member = await seed.Ctx.LoyaltyMembers.SingleAsync();
        Assert.Equal(20, member.PointsBalance);
        Assert.Equal(1, member.VisitCount);
        Assert.Equal(200m, member.TotalSpent);
        Assert.Equal(5, await seed.Ctx.LoyaltyTransactions.CountAsync()); // earn, redeem, earn, plus two reversals
    }

    [Fact]
    public async Task Loyalty_RefusesWhenDisabled_AndBadRedeemAmounts()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);
        var order = await OpenPizzaOrderAsync(service, seed, 1);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SetOrderLoyaltyAsync(order.Id, new SetOrderLoyaltyDto { Phone = "01001234567" }));

        await EnableLoyaltyAsync(seed.Ctx);
        await service.SetOrderLoyaltyAsync(order.Id, new SetOrderLoyaltyDto { Phone = "01001234567", Name = "منى" });
        // new customer has no points yet, so any redemption is refused; below the minimum is refused too
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SetOrderLoyaltyAsync(order.Id, new SetOrderLoyaltyDto { Phone = "01001234567", PointsToRedeem = 5 }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SetOrderLoyaltyAsync(order.Id, new SetOrderLoyaltyDto { Phone = "01001234567", PointsToRedeem = 50 }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SetOrderLoyaltyAsync(order.Id, new SetOrderLoyaltyDto { Phone = "12" }));
    }

    [Fact]
    public async Task Loyalty_ReturningCustomerIsAttachedAutomaticallyByPhone_AndHistoryListsOrders()
    {
        var seed = await SeedAsync();
        await EnableLoyaltyAsync(seed.Ctx);
        var service = BuildService(seed.Ctx);

        var first = await OpenPizzaOrderAsync(service, seed, 1);
        await service.SetOrderLoyaltyAsync(first.Id, new SetOrderLoyaltyDto { Phone = "01001234567", Name = "سارة" });
        await service.BillOrderAsync(first.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });

        var takeaway = await service.CreateOrderAsync(new CreateRestaurantOrderDto
        {
            OrderType = RestaurantOrderType.Takeaway, CustomerPhone = "00201001234567", WarehouseId = seed.Warehouse.Id
        });
        Assert.NotNull(takeaway.LoyaltyMemberId);
        Assert.Equal("سارة", takeaway.LoyaltyMemberName);

        var loyalty = new LoyaltyService(seed.Ctx);
        var member = (await loyalty.GetMembersAsync("سارة")).Single();
        var detail = await loyalty.GetMemberAsync(member.Id);
        Assert.Single(detail.Orders);
        Assert.Equal(10, detail.Orders[0].PointsEarned);
        Assert.Contains("بيتزا × 1", detail.FavouriteItems);
    }

    [Fact]
    public void LoyaltyRules_CapRedemptionToWhatIsLeftToPay()
    {
        var settings = new LoyaltySettings(true, 10m, 0.5m, 20);
        var order = new RestaurantOrder { LoyaltyMemberId = Guid.NewGuid(), LoyaltyPointsToRedeem = 100 }; // worth 50
        var (points, discount) = LoyaltyRules.Redemption(order, 30m, settings);
        Assert.Equal(30m, discount);
        Assert.Equal(60, points);
        Assert.Equal((0, 0m), LoyaltyRules.Redemption(order, 30m, settings with { Enabled = false }));
        Assert.Equal(4, LoyaltyRules.PointsEarned(49.99m, settings));
    }

    private static OfflineOrderDto OfflineTicket(SeedResult seed, decimal pizzas, DateTime? createdAt = null) => new()
    {
        ClientId = Guid.NewGuid(),
        CreatedAtUtc = createdAt ?? DateTime.UtcNow.AddHours(-2),
        OrderType = RestaurantOrderType.DineIn,
        TableId = seed.Table.Id,
        WarehouseId = seed.Warehouse.Id,
        PaymentTerm = PaymentTerm.Cash,
        FiscalPeriodId = seed.Period.Id,
        Lines = new List<OfflineOrderLineDto> { new() { ItemId = seed.Pizza.Id, Quantity = pizzas } }
    };

    [Fact]
    public async Task OfflineSync_BillsTicketOnItsOwnDate_AndIgnoresAResend()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);
        var saleDay = DateTime.UtcNow.Date.AddDays(-1).AddHours(20);
        var ticket = OfflineTicket(seed, 2, saleDay);

        var first = (await service.SyncOfflineOrdersAsync(new OfflineOrdersBatchDto { Orders = { ticket } })).Single();
        Assert.True(first.Success, first.Error);
        Assert.False(first.AlreadySynced);
        Assert.NotNull(first.InvoiceNumber);

        var invoice = await seed.Ctx.SalesInvoices.SingleAsync();
        Assert.Equal(saleDay.Date, invoice.InvoiceDate);
        Assert.Equal(228m, invoice.TotalAmount); // 2 x 100 + 14% VAT
        // The table was not touched: the diners are long gone and nobody is seated there.
        Assert.Equal(RestaurantTableStatus.Available, (await seed.Ctx.RestaurantTables.SingleAsync()).Status);
        Assert.Equal(99m, (await seed.Ctx.Items.SingleAsync(i => i.Id == seed.Flour.Id)).QuantityOnHand); // 2 x 0.5 kg used

        // The terminal lost the answer and sends the same ticket again: nothing is billed twice.
        var again = (await service.SyncOfflineOrdersAsync(new OfflineOrdersBatchDto { Orders = { ticket } })).Single();
        Assert.True(again.Success);
        Assert.True(again.AlreadySynced);
        Assert.Equal(first.OrderNumber, again.OrderNumber);
        Assert.Equal(1, await seed.Ctx.SalesInvoices.CountAsync());
    }

    [Fact]
    public async Task OfflineSync_FailedTicketIsRolledBack_DoesNotBlockOthers_AndCanBeRetried()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);

        var tooBig = OfflineTicket(seed, 500); // needs 250 kg of flour, only 100 on hand
        var fine = OfflineTicket(seed, 1);

        var results = await service.SyncOfflineOrdersAsync(new OfflineOrdersBatchDto { Orders = { tooBig, fine } });
        var failed = results.Single(r => r.ClientId == tooBig.ClientId);
        var ok = results.Single(r => r.ClientId == fine.ClientId);
        Assert.False(failed.Success);
        Assert.False(string.IsNullOrWhiteSpace(failed.Error));
        Assert.True(ok.Success, ok.Error);
        Assert.Equal(1, await seed.Ctx.SalesInvoices.CountAsync());

        // Stock arrives; the same ticket syncs fine on the next attempt.
        var flour = await seed.Ctx.Items.SingleAsync(i => i.Id == seed.Flour.Id);
        flour.QuantityOnHand = 1000;
        await seed.Ctx.SaveChangesAsync();
        var retry = (await service.SyncOfflineOrdersAsync(new OfflineOrdersBatchDto { Orders = { tooBig } })).Single();
        Assert.True(retry.Success, retry.Error);
        Assert.Equal(2, await seed.Ctx.SalesInvoices.CountAsync());
    }

    [Fact]
    public async Task OfflineSync_RejectsEmptyAndCreditTickets()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx);
        var empty = OfflineTicket(seed, 1);
        empty.Lines.Clear();
        var credit = OfflineTicket(seed, 1);
        credit.PaymentTerm = PaymentTerm.Credit;

        var results = await service.SyncOfflineOrdersAsync(new OfflineOrdersBatchDto { Orders = { empty, credit } });
        Assert.All(results, r => Assert.False(r.Success));
        Assert.Equal(0, await seed.Ctx.SalesInvoices.CountAsync());
    }
}
