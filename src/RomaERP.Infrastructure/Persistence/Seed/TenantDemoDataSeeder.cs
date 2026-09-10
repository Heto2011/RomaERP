using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Common;

namespace RomaERP.Infrastructure.Persistence.Seed;

/// <summary>Optional, opt-in seeding for sales-demo tenants only — populates a freshly-provisioned tenant
/// with a small, realistic F&amp;B dataset (raw materials, a recipe-based menu item, a vendor + purchase,
/// a customer + sale, a billed restaurant order) so every report and dashboard has real numbers to show a
/// prospect immediately, instead of a blank slate. Goes through the same application services a real user
/// would hit, so every posted amount is genuinely GL-correct — nothing here is fabricated report data.</summary>
public static class TenantDemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ApplicationDbContext context, CancellationToken ct = default)
    {
        var itemService = services.GetRequiredService<IItemService>();
        var inventoryService = services.GetRequiredService<IInventoryService>();
        var restaurantService = services.GetRequiredService<IRestaurantService>();
        var salesService = services.GetRequiredService<ISalesService>();
        var purchasingService = services.GetRequiredService<IPurchasingService>();

        var warehouse = await context.Warehouses.AsNoTracking().FirstAsync(ct);
        var category = await context.ItemCategories.AsNoTracking().FirstAsync(ct);
        var today = DateTime.UtcNow.Date;
        var period = await context.FiscalPeriods.AsNoTracking().FirstAsync(p => p.StartDate <= today && p.EndDate >= today, ct);

        var flour = await itemService.CreateAsync(new CreateItemDto { Code = "RAW-FLOUR", NameAr = "دقيق", NameEn = "Flour", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, ReorderLevel = 10 }, ct);
        var cheese = await itemService.CreateAsync(new CreateItemDto { Code = "RAW-CHEESE", NameAr = "جبنة موتزاريلا", NameEn = "Mozzarella Cheese", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, ReorderLevel = 5 }, ct);
        var sauce = await itemService.CreateAsync(new CreateItemDto { Code = "RAW-SAUCE", NameAr = "صلصة طماطم", NameEn = "Tomato Sauce", UnitOfMeasure = "كجم", ItemCategoryId = category.Id, ReorderLevel = 5 }, ct);
        var drink = await itemService.CreateAsync(new CreateItemDto { Code = "MENU-DRINK", NameAr = "عصير برتقال", NameEn = "Orange Juice", UnitOfMeasure = "قطعة", ItemCategoryId = category.Id, ReorderLevel = 20 }, ct);

        await inventoryService.ReceiveStockAsync(new ReceiveStockDto { MovementDate = today.AddDays(-20), FiscalPeriodId = period.Id, ItemId = flour.Id, WarehouseId = warehouse.Id, Quantity = 50, UnitCost = 8, Reference = "DEMO", Description = "استلام مخزون افتتاحي" }, ct);
        await inventoryService.ReceiveStockAsync(new ReceiveStockDto { MovementDate = today.AddDays(-20), FiscalPeriodId = period.Id, ItemId = cheese.Id, WarehouseId = warehouse.Id, Quantity = 20, UnitCost = 45, Reference = "DEMO", Description = "استلام مخزون افتتاحي" }, ct);
        await inventoryService.ReceiveStockAsync(new ReceiveStockDto { MovementDate = today.AddDays(-20), FiscalPeriodId = period.Id, ItemId = sauce.Id, WarehouseId = warehouse.Id, Quantity = 15, UnitCost = 12, Reference = "DEMO", Description = "استلام مخزون افتتاحي" }, ct);
        await inventoryService.ReceiveStockAsync(new ReceiveStockDto { MovementDate = today.AddDays(-20), FiscalPeriodId = period.Id, ItemId = drink.Id, WarehouseId = warehouse.Id, Quantity = 100, UnitCost = 3, Reference = "DEMO", Description = "استلام مخزون افتتاحي" }, ct);

        var pizza = await itemService.CreateAsync(new CreateItemDto { Code = "MENU-PIZZA", NameAr = "بيتزا مارجريتا", NameEn = "Margherita Pizza", UnitOfMeasure = "قطعة", ItemCategoryId = category.Id, ReorderLevel = 0 }, ct);
        await restaurantService.SetMenuItemAsync(pizza.Id, new SetMenuItemDto
        {
            IsMenuItem = true,
            MenuPrice = 45,
            RecipeLines = new List<SetRecipeLineInputDto>
            {
                new() { RawMaterialItemId = flour.Id, QuantityPerUnit = 0.3m },
                new() { RawMaterialItemId = cheese.Id, QuantityPerUnit = 0.2m },
                new() { RawMaterialItemId = sauce.Id, QuantityPerUnit = 0.1m }
            }
        }, ct);
        await restaurantService.SetMenuItemAsync(drink.Id, new SetMenuItemDto { IsMenuItem = true, MenuPrice = 12, RecipeLines = new List<SetRecipeLineInputDto>() }, ct);

        var vendor = await purchasingService.CreateVendorAsync(new CreateVendorDto { Code = "VEND-001", NameAr = "شركة التوريدات الغذائية", NameEn = "Food Supplies Co.", Phone = "0500000000" }, ct);
        var inventoryAccount = await context.Accounts.AsNoTracking().FirstAsync(a => a.Code == "1160", ct);
        await purchasingService.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
        {
            VendorId = vendor.Id,
            InvoiceDate = today.AddDays(-15),
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = new List<PurchaseInvoiceLineInputDto>
            {
                new() { Description = "دقيق - دفعة إضافية", AccountId = inventoryAccount.Id, ItemId = flour.Id, Quantity = 20, UnitPrice = 8 }
            }
        }, ct);

        var customer = await salesService.CreateCustomerAsync(new CreateCustomerDto { Code = "CUST-001", NameAr = "عميل تجريبي", NameEn = "Demo Customer", Phone = "0511111111" }, ct);
        await salesService.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = today.AddDays(-5),
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            WarehouseId = warehouse.Id,
            Lines = new List<SalesInvoiceLineInputDto>
            {
                new() { Description = "عصير برتقال", ItemId = drink.Id, Quantity = 10, UnitPrice = 12 }
            }
        }, ct);

        var table = await restaurantService.CreateTableAsync(new CreateRestaurantTableDto { Number = "T1", SectionName = "الصالة", Capacity = 4 }, ct);
        var order = await restaurantService.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = Domain.Restaurant.RestaurantOrderType.DineIn, TableId = table.Id, WarehouseId = warehouse.Id }, ct);
        await restaurantService.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = pizza.Id, Quantity = 2 }, ct);
        await restaurantService.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = drink.Id, Quantity = 2 }, ct);
        await restaurantService.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = period.Id }, ct);
    }
}
