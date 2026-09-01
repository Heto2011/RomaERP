using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class PurchasingServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Account cash, Account bank, Account ap, Account expense, Account inputVat, Vendor vendor, FiscalPeriod period)> SeedAsync(decimal vatRate = 0.14m)
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var bank = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var ap = new Account { Code = "2120", NameAr = "الموردون", NameEn = "AP", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var expense = new Account { Code = "5300", NameAr = "مصروفات إدارية", NameEn = "Admin Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var inputVat = new Account { Code = "1180", NameAr = "ضريبة مدخلات", NameEn = "Input VAT", AccountType = AccountType.Asset, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };

        var vendor = new Vendor { Code = "VEND-1", NameAr = "مورد تجريبي", NameEn = "Test Vendor" };

        ctx.Accounts.AddRange(cash, bank, ap, expense, inputVat);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        ctx.Vendors.Add(vendor);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = vatRate, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        return (ctx, cash, bank, ap, expense, inputVat, vendor, period);
    }

    [Fact]
    public async Task CreateInvoice_WithCashTerm_SettlesImmediatelyWithoutTouchingAp()
    {
        var (ctx, cash, _, _, expense, inputVat, vendor, period) = await SeedAsync();
        var service = new PurchasingService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
        {
            VendorId = vendor.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            Lines = { new PurchaseInvoiceLineInputDto { Description = "إيجار", AccountId = expense.Id, Quantity = 1, UnitPrice = 300 } }
        });

        Assert.Equal(300, invoice.SubTotal);
        Assert.Equal(42, invoice.VatAmount);
        Assert.Equal(342, invoice.TotalAmount);
        Assert.Equal(342, invoice.PaidAmount);
        Assert.Equal(0, invoice.OutstandingAmount);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == expense.Id && l.Debit == 300);
        Assert.Contains(entry.Lines, l => l.AccountId == inputVat.Id && l.Debit == 42);
        Assert.Contains(entry.Lines, l => l.AccountId == cash.Id && l.Credit == 342);

        var updatedVendor = await ctx.Vendors.FirstAsync(v => v.Id == vendor.Id);
        Assert.Equal(0, updatedVendor.ApBalance);
    }

    [Fact]
    public async Task CreateInvoice_WithCreditTerm_PostsToApAndIncreasesVendorBalance()
    {
        var (ctx, _, _, ap, expense, inputVat, vendor, period) = await SeedAsync();
        var service = new PurchasingService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
        {
            VendorId = vendor.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new PurchaseInvoiceLineInputDto { Description = "خدمات", AccountId = expense.Id, Quantity = 2, UnitPrice = 250 } }
        });

        Assert.Equal(0, invoice.PaidAmount);
        Assert.Equal(570, invoice.TotalAmount);
        Assert.Equal(570, invoice.OutstandingAmount);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == expense.Id && l.Debit == 500);
        Assert.Contains(entry.Lines, l => l.AccountId == inputVat.Id && l.Debit == 70);
        Assert.Contains(entry.Lines, l => l.AccountId == ap.Id && l.Credit == 570);

        var updatedVendor = await ctx.Vendors.FirstAsync(v => v.Id == vendor.Id);
        Assert.Equal(570, updatedVendor.ApBalance);
    }

    [Fact]
    public async Task RecordPayment_OnCreditInvoice_ReducesOutstandingAndVendorBalance()
    {
        var (ctx, cash, _, ap, expense, _, vendor, period) = await SeedAsync();
        var service = new PurchasingService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
        {
            VendorId = vendor.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new PurchaseInvoiceLineInputDto { Description = "خدمات", AccountId = expense.Id, Quantity = 1, UnitPrice = 1000 } }
        });

        var updated = await service.RecordPaymentAsync(invoice.Id, new RecordPurchasePaymentDto
        {
            Amount = 400,
            Method = PaymentTerm.Cash,
            PaymentDate = DateTime.UtcNow.Date
        });

        Assert.Equal(400, updated.PaidAmount);
        Assert.Equal(740, updated.OutstandingAmount); // 1140 total - 400 paid

        var updatedVendor = await ctx.Vendors.FirstAsync(v => v.Id == vendor.Id);
        Assert.Equal(740, updatedVendor.ApBalance);

        var paymentEntry = await ctx.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.Reference == "PURCHASE-INVOICE")
            .OrderByDescending(e => e.EntryNumber)
            .FirstAsync();
        Assert.Contains(paymentEntry.Lines, l => l.AccountId == ap.Id && l.Debit == 400);
        Assert.Contains(paymentEntry.Lines, l => l.AccountId == cash.Id && l.Credit == 400);
    }

    [Fact]
    public async Task GetApAging_BucketsOutstandingInvoicesByAge()
    {
        var (ctx, _, _, _, expense, _, vendor, period) = await SeedAsync();
        var service = new PurchasingService(ctx, new FakeHtmlToPdfRenderer());
        var today = DateTime.UtcNow.Date;

        async Task<PurchaseInvoiceDto> CreateAt(DateTime invoiceDate, decimal unitPrice)
            => await service.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
            {
                VendorId = vendor.Id,
                InvoiceDate = invoiceDate,
                FiscalPeriodId = period.Id,
                PaymentTerm = PaymentTerm.Credit,
                Lines = { new PurchaseInvoiceLineInputDto { Description = "بند", AccountId = expense.Id, Quantity = 1, UnitPrice = unitPrice } }
            });

        var current = await CreateAt(today, 100);
        var d40 = await CreateAt(today.AddDays(-40), 200);
        var d75 = await CreateAt(today.AddDays(-75), 300);
        var d120 = await CreateAt(today.AddDays(-120), 400);

        await service.RecordPaymentAsync(d40.Id, new RecordPurchasePaymentDto { Amount = 50, Method = PaymentTerm.Cash, PaymentDate = today });

        var aging = await service.GetApAgingAsync(today);

        var row = Assert.Single(aging);
        Assert.Equal(vendor.Id, row.VendorId);
        Assert.Equal(current.TotalAmount + (d40.TotalAmount - 50) + d75.TotalAmount + d120.TotalAmount, row.TotalOutstanding);
        Assert.Equal(current.TotalAmount, row.Current);
        Assert.Equal(d40.TotalAmount - 50, row.Days31To60);
        Assert.Equal(d75.TotalAmount, row.Days61To90);
        Assert.Equal(d120.TotalAmount, row.Over90Days);
    }

    [Fact]
    public async Task GetApAging_ExcludesCashInvoicesAndFullyPaidCreditInvoices()
    {
        var (ctx, _, _, _, expense, _, vendor, period) = await SeedAsync();
        var service = new PurchasingService(ctx, new FakeHtmlToPdfRenderer());

        await service.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
        {
            VendorId = vendor.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            Lines = { new PurchaseInvoiceLineInputDto { Description = "بند", AccountId = expense.Id, Quantity = 1, UnitPrice = 100 } }
        });

        var creditInvoice = await service.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
        {
            VendorId = vendor.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new PurchaseInvoiceLineInputDto { Description = "بند", AccountId = expense.Id, Quantity = 1, UnitPrice = 100 } }
        });
        await service.RecordPaymentAsync(creditInvoice.Id, new RecordPurchasePaymentDto { Amount = creditInvoice.TotalAmount, Method = PaymentTerm.Cash, PaymentDate = DateTime.UtcNow.Date });

        var aging = await service.GetApAgingAsync();

        Assert.Empty(aging);
    }

    [Fact]
    public async Task ReceiveInventoryPurchase_UpdatesItemCostAndPostsInvoiceWithVat()
    {
        var (ctx, _, _, ap, _, inputVat, vendor, period) = await SeedAsync();
        var inventoryAccount = new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var category = new ItemCategory { Code = "CAT-1", NameAr = "فئة", NameEn = "Category" };
        var warehouse = new Warehouse { Code = "WH-1", NameAr = "المخزن الرئيسي", NameEn = "Main Warehouse" };
        var item = new Item { Code = "ITM-1", NameAr = "دقيق", NameEn = "Flour", UnitOfMeasure = "kg", ItemCategoryId = category.Id, ItemCategory = category, QuantityOnHand = 10, AverageCost = 5 };
        ctx.Accounts.Add(inventoryAccount);
        ctx.ItemCategories.Add(category);
        ctx.Warehouses.Add(warehouse);
        ctx.Items.Add(item);
        await ctx.SaveChangesAsync();

        var service = new PurchasingService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.ReceiveInventoryPurchaseAsync(new ReceiveInventoryPurchaseDto
        {
            VendorId = vendor.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            WarehouseId = warehouse.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new ReceiveInventoryPurchaseLineInputDto { ItemId = item.Id, Quantity = 10, UnitCost = 8 } }
        });

        // Weighted average: (10*5 + 10*8) / 20 = 6.5
        var updatedItem = await ctx.Items.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(20, updatedItem.QuantityOnHand);
        Assert.Equal(6.5m, updatedItem.AverageCost);

        Assert.Equal(80, invoice.SubTotal);
        Assert.Equal(11.2m, invoice.VatAmount); // 80 * 0.14
        Assert.Equal(91.2m, invoice.TotalAmount);
        Assert.Equal(0, invoice.PaidAmount);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == inventoryAccount.Id && l.Debit == 80);
        Assert.Contains(entry.Lines, l => l.AccountId == inputVat.Id && l.Debit == 11.2m);
        Assert.Contains(entry.Lines, l => l.AccountId == ap.Id && l.Credit == 91.2m);

        var updatedVendor = await ctx.Vendors.FirstAsync(v => v.Id == vendor.Id);
        Assert.Equal(91.2m, updatedVendor.ApBalance);

        var movement = await ctx.StockMovements.FirstAsync(m => m.ItemId == item.Id);
        Assert.Equal(StockMovementType.Receipt, movement.MovementType);
        Assert.Equal(10, movement.Quantity);
        Assert.Equal(8, movement.UnitCost);
        Assert.Equal(entry.Id, movement.JournalEntryId);
    }
}
