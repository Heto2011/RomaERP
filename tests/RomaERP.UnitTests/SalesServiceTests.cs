using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>No-op stand-in for the real Playwright-backed renderer — SalesServiceTests exercises invoice
/// business logic, not PDF rendering (see SalesInvoicePdfTests for that).</summary>
public class FakeHtmlToPdfRenderer : IHtmlToPdfRenderer
{
    public Task<byte[]> RenderAsync(string html, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
}

public class SalesServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Account cash, Account bank, Account ar, Account revenue, Account outputVat, Customer customer, FiscalPeriod period)> SeedAsync(decimal vatRate = 0.14m)
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var bank = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var ar = new Account { Code = "1120", NameAr = "العملاء", NameEn = "AR", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var outputVat = new Account { Code = "2161", NameAr = "ضريبة مخرجات", NameEn = "Output VAT", AccountType = AccountType.Liability, Nature = AccountNature.Credit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };

        var customer = new Customer { Code = "CUST-1", NameAr = "عميل تجريبي", NameEn = "Test Customer" };

        ctx.Accounts.AddRange(cash, bank, ar, revenue, outputVat);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        ctx.Customers.Add(customer);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = vatRate, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        return (ctx, cash, bank, ar, revenue, outputVat, customer, period);
    }

    private static async Task<(Account cogs, Account inventory, Item item, Warehouse warehouse)> AddInventoryAsync(ApplicationDbContext ctx, decimal quantityOnHand, decimal averageCost)
    {
        var cogs = new Account { Code = "5500", NameAr = "تكلفة البضاعة المباعة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var inventory = new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var category = new ItemCategory { Code = "CAT-1", NameAr = "تصنيف", NameEn = "Category" };
        var warehouse = new Warehouse { Code = "WH-1", NameAr = "المخزن الرئيسي", NameEn = "Main Warehouse" };

        ctx.Accounts.AddRange(cogs, inventory);
        ctx.ItemCategories.Add(category);
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITEM-1",
            NameAr = "منتج تجريبي",
            NameEn = "Test Item",
            UnitOfMeasure = "قطعة",
            ItemCategoryId = category.Id,
            QuantityOnHand = quantityOnHand,
            AverageCost = averageCost
        };
        ctx.Items.Add(item);
        await ctx.SaveChangesAsync();

        return (cogs, inventory, item, warehouse);
    }

    [Fact]
    public async Task CreateInvoice_WithCashTerm_SettlesImmediatelyWithoutTouchingAr()
    {
        var (ctx, cash, _, _, revenue, outputVat, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 2, UnitPrice = 100 } }
        });

        Assert.Equal(200, invoice.SubTotal);
        Assert.Equal(28, invoice.VatAmount);
        Assert.Equal(228, invoice.TotalAmount);
        Assert.Equal(228, invoice.PaidAmount);
        Assert.Equal(0, invoice.OutstandingAmount);
        Assert.NotNull(invoice.JournalEntryId);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == cash.Id && l.Debit == 228);
        Assert.Contains(entry.Lines, l => l.AccountId == revenue.Id && l.Credit == 200);
        Assert.Contains(entry.Lines, l => l.AccountId == outputVat.Id && l.Credit == 28);

        var updatedCustomer = await ctx.Customers.FirstAsync(c => c.Id == customer.Id);
        Assert.Equal(0, updatedCustomer.ArBalance);
    }

    [Fact]
    public async Task CreateInvoice_WithCreditTerm_PostsToArAndIncreasesCustomerBalance()
    {
        var (ctx, _, _, ar, revenue, outputVat, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 500 } }
        });

        Assert.Equal(0, invoice.PaidAmount);
        Assert.Equal(570, invoice.TotalAmount);
        Assert.Equal(570, invoice.OutstandingAmount);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == ar.Id && l.Debit == 570);
        Assert.Contains(entry.Lines, l => l.AccountId == revenue.Id && l.Credit == 500);
        Assert.Contains(entry.Lines, l => l.AccountId == outputVat.Id && l.Credit == 70);

        var updatedCustomer = await ctx.Customers.FirstAsync(c => c.Id == customer.Id);
        Assert.Equal(570, updatedCustomer.ArBalance);
    }

    [Fact]
    public async Task CreateInvoice_WithInstallmentTerm_PostsToArAndGeneratesEqualMonthlySchedule()
    {
        var (ctx, _, _, ar, revenue, outputVat, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());
        var firstDue = DateTime.UtcNow.Date;

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Installment,
            NumberOfInstallments = 3,
            FirstInstallmentDueDate = firstDue,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 500 } }
        });

        Assert.Equal(0, invoice.PaidAmount);
        Assert.Equal(570, invoice.TotalAmount);
        Assert.Equal(570, invoice.OutstandingAmount);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == ar.Id && l.Debit == 570);
        Assert.Contains(entry.Lines, l => l.AccountId == revenue.Id && l.Credit == 500);
        Assert.Contains(entry.Lines, l => l.AccountId == outputVat.Id && l.Credit == 70);

        Assert.Equal(3, invoice.InstallmentLines.Count);
        Assert.Equal(190, invoice.InstallmentLines[0].Amount);
        Assert.Equal(firstDue, invoice.InstallmentLines[0].DueDate);
        Assert.Equal(firstDue.AddMonths(1), invoice.InstallmentLines[1].DueDate);
        Assert.Equal(firstDue.AddMonths(2), invoice.InstallmentLines[2].DueDate);
        Assert.All(invoice.InstallmentLines, l => Assert.False(l.IsPaid));

        var updated = await service.RecordPaymentAsync(invoice.Id, new RecordSalesPaymentDto
        {
            Amount = 190,
            Method = PaymentTerm.Cash,
            PaymentDate = DateTime.UtcNow.Date
        });

        Assert.True(updated.InstallmentLines[0].IsPaid);
        Assert.False(updated.InstallmentLines[1].IsPaid);
    }

    [Fact]
    public async Task CreateInvoice_WithInstallmentTerm_RequiresScheduleFields()
    {
        var (ctx, _, _, _, _, _, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Installment,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 500 } }
        }));
    }

    [Fact]
    public async Task RecordPayment_OnCreditInvoice_ReducesOutstandingAndCustomerBalance()
    {
        var (ctx, _, bank, ar, _, _, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 1000 } }
        });

        var updated = await service.RecordPaymentAsync(invoice.Id, new RecordSalesPaymentDto
        {
            Amount = 600,
            Method = PaymentTerm.Card,
            PaymentDate = DateTime.UtcNow.Date
        });

        Assert.Equal(600, updated.PaidAmount);
        Assert.Equal(540, updated.OutstandingAmount); // 1140 total - 600 paid

        var updatedCustomer = await ctx.Customers.FirstAsync(c => c.Id == customer.Id);
        Assert.Equal(540, updatedCustomer.ArBalance);

        var paymentEntry = await ctx.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.Reference == "SALES-INVOICE")
            .OrderByDescending(e => e.EntryNumber)
            .FirstAsync();
        Assert.Contains(paymentEntry.Lines, l => l.AccountId == bank.Id && l.Debit == 600);
        Assert.Contains(paymentEntry.Lines, l => l.AccountId == ar.Id && l.Credit == 600);
    }

    [Fact]
    public async Task RecordPayment_ExceedingOutstanding_Throws()
    {
        var (ctx, _, _, _, _, _, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 100 } }
        });

        await Assert.ThrowsAsync<ValidationAppException>(
            () => service.RecordPaymentAsync(invoice.Id, new RecordSalesPaymentDto { Amount = 1000, Method = PaymentTerm.Cash, PaymentDate = DateTime.UtcNow.Date }));
    }

    [Fact]
    public async Task GetArAging_BucketsOutstandingInvoicesByAge()
    {
        var (ctx, _, _, _, _, _, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());
        var today = DateTime.UtcNow.Date;

        async Task<SalesInvoiceDto> CreateAt(DateTime invoiceDate, decimal unitPrice)
            => await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = customer.Id,
                InvoiceDate = invoiceDate,
                FiscalPeriodId = period.Id,
                PaymentTerm = PaymentTerm.Credit,
                Lines = { new SalesInvoiceLineInputDto { Description = "بند", Quantity = 1, UnitPrice = unitPrice } }
            });

        var current = await CreateAt(today, 100);          // age 0 -> Current
        var d40 = await CreateAt(today.AddDays(-40), 200);   // age 40 -> 31-60
        var d75 = await CreateAt(today.AddDays(-75), 300);   // age 75 -> 61-90
        var d120 = await CreateAt(today.AddDays(-120), 400); // age 120 -> Over90

        // Partially collect the 40-day-old invoice; only the remaining balance should still age.
        await service.RecordPaymentAsync(d40.Id, new RecordSalesPaymentDto { Amount = 50, Method = PaymentTerm.Cash, PaymentDate = today });

        var aging = await service.GetArAgingAsync(today);

        var row = Assert.Single(aging);
        Assert.Equal(customer.Id, row.CustomerId);
        Assert.Equal(current.TotalAmount + (d40.TotalAmount - 50) + d75.TotalAmount + d120.TotalAmount, row.TotalOutstanding);
        Assert.Equal(current.TotalAmount, row.Current);
        Assert.Equal(d40.TotalAmount - 50, row.Days31To60);
        Assert.Equal(d75.TotalAmount, row.Days61To90);
        Assert.Equal(d120.TotalAmount, row.Over90Days);
    }

    [Fact]
    public async Task GetArAging_ExcludesCashInvoicesAndFullyPaidCreditInvoices()
    {
        var (ctx, _, _, _, _, _, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            Lines = { new SalesInvoiceLineInputDto { Description = "بند", Quantity = 1, UnitPrice = 100 } }
        });

        var creditInvoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new SalesInvoiceLineInputDto { Description = "بند", Quantity = 1, UnitPrice = 100 } }
        });
        await service.RecordPaymentAsync(creditInvoice.Id, new RecordSalesPaymentDto { Amount = creditInvoice.TotalAmount, Method = PaymentTerm.Cash, PaymentDate = DateTime.UtcNow.Date });

        var aging = await service.GetArAgingAsync();

        Assert.Empty(aging);
    }

    [Fact]
    public async Task CreateInvoice_WithItemLine_IssuesStockAndPostsCogs()
    {
        var (ctx, cash, _, _, revenue, outputVat, customer, period) = await SeedAsync();
        var (cogs, inventory, item, warehouse) = await AddInventoryAsync(ctx, quantityOnHand: 50, averageCost: 30);
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            WarehouseId = warehouse.Id,
            Lines = { new SalesInvoiceLineInputDto { Description = "بيع منتج", Quantity = 5, UnitPrice = 100, ItemId = item.Id } }
        });

        Assert.Equal(500, invoice.SubTotal);
        Assert.Single(invoice.Lines);
        Assert.Equal(item.Id, invoice.Lines[0].ItemId);
        Assert.Equal("ITEM-1", invoice.Lines[0].ItemCode);

        // Revenue side: unaffected by the inventory linkage.
        var revenueEntry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Contains(revenueEntry.Lines, l => l.AccountId == cash.Id && l.Debit == 570);
        Assert.Contains(revenueEntry.Lines, l => l.AccountId == revenue.Id && l.Credit == 500);
        Assert.Contains(revenueEntry.Lines, l => l.AccountId == outputVat.Id && l.Credit == 70);

        // COGS side: 5 units * 30 average cost = 150, posted as a separate balanced entry.
        var cogsEntry = await ctx.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.Reference == "SALES-INVOICE" && e.Id != invoice.JournalEntryId)
            .SingleAsync();
        Assert.Contains(cogsEntry.Lines, l => l.AccountId == cogs.Id && l.Debit == 150);
        Assert.Contains(cogsEntry.Lines, l => l.AccountId == inventory.Id && l.Credit == 150);
        Assert.Equal(cogsEntry.TotalDebit, cogsEntry.TotalCredit);

        var updatedItem = await ctx.Items.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(45, updatedItem.QuantityOnHand);

        var movement = await ctx.StockMovements.SingleAsync(m => m.ItemId == item.Id);
        Assert.Equal(StockMovementType.Issue, movement.MovementType);
        Assert.Equal(5, movement.Quantity);
        Assert.Equal(30, movement.UnitCost);
        Assert.Equal(invoice.InvoiceNumber, movement.Reference);
        Assert.Equal(cogsEntry.Id, movement.JournalEntryId);
    }

    [Fact]
    public async Task CreateInvoice_WithItemLine_InsufficientStock_Throws()
    {
        var (ctx, _, _, _, _, _, customer, period) = await SeedAsync();
        var (_, _, item, warehouse) = await AddInventoryAsync(ctx, quantityOnHand: 3, averageCost: 30);
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            WarehouseId = warehouse.Id,
            Lines = { new SalesInvoiceLineInputDto { Description = "بيع منتج", Quantity = 10, UnitPrice = 100, ItemId = item.Id } }
        }));

        var unchangedItem = await ctx.Items.FirstAsync(i => i.Id == item.Id);
        Assert.Equal(3, unchangedItem.QuantityOnHand);
    }

    [Fact]
    public async Task CreateNote_Credit_DecreasesCustomerArBalanceAndDebitsRevenue()
    {
        var (ctx, _, _, ar, revenue, outputVat, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 500 } }
        });
        // invoice.TotalAmount = 570, customer.ArBalance = 570

        var note = await service.CreateNoteAsync(new CreateSalesNoteDto
        {
            OriginalInvoiceId = invoice.Id,
            NoteType = SalesNoteType.Credit,
            NoteDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            Reason = "إرجاع بضاعة تالفة",
            Lines = { new SalesNoteLineInputDto { Description = "إرجاع", Quantity = 1, UnitPrice = 100 } }
        });

        Assert.StartsWith("CN-", note.NoteNumber);
        Assert.Equal(100, note.SubTotal);
        Assert.Equal(14, note.VatAmount);
        Assert.Equal(114, note.TotalAmount);
        Assert.Equal(invoice.InvoiceNumber, note.OriginalInvoiceNumber);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == note.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == revenue.Id && l.Debit == 100);
        Assert.Contains(entry.Lines, l => l.AccountId == outputVat.Id && l.Debit == 14);
        Assert.Contains(entry.Lines, l => l.AccountId == ar.Id && l.Credit == 114);

        var updatedCustomer = await ctx.Customers.FirstAsync(c => c.Id == customer.Id);
        Assert.Equal(570 - 114, updatedCustomer.ArBalance);
    }

    [Fact]
    public async Task CreateNote_Debit_IncreasesCustomerArBalanceAndCreditsRevenue()
    {
        var (ctx, _, _, ar, revenue, outputVat, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 500 } }
        });

        var note = await service.CreateNoteAsync(new CreateSalesNoteDto
        {
            OriginalInvoiceId = invoice.Id,
            NoteType = SalesNoteType.Debit,
            NoteDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            Reason = "رسوم شحن إضافية",
            Lines = { new SalesNoteLineInputDto { Description = "شحن", Quantity = 1, UnitPrice = 50 } }
        });

        Assert.StartsWith("DN-", note.NoteNumber);
        Assert.Equal(50, note.SubTotal);
        Assert.Equal(7, note.VatAmount);
        Assert.Equal(57, note.TotalAmount);

        var entry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == note.JournalEntryId);
        Assert.Contains(entry.Lines, l => l.AccountId == ar.Id && l.Debit == 57);
        Assert.Contains(entry.Lines, l => l.AccountId == revenue.Id && l.Credit == 50);
        Assert.Contains(entry.Lines, l => l.AccountId == outputVat.Id && l.Credit == 7);

        var updatedCustomer = await ctx.Customers.FirstAsync(c => c.Id == customer.Id);
        Assert.Equal(570 + 57, updatedCustomer.ArBalance);
    }

    [Fact]
    public async Task CreateNote_WithoutReason_Throws()
    {
        var (ctx, _, _, _, _, _, customer, period) = await SeedAsync();
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 100 } }
        });

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateNoteAsync(new CreateSalesNoteDto
        {
            OriginalInvoiceId = invoice.Id,
            NoteType = SalesNoteType.Credit,
            NoteDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            Reason = "",
            Lines = { new SalesNoteLineInputDto { Description = "إرجاع", Quantity = 1, UnitPrice = 10 } }
        }));
    }

    [Fact]
    public async Task CreateInvoice_WithItemLineButNoWarehouse_Throws()
    {
        var (ctx, _, _, _, _, _, customer, period) = await SeedAsync();
        var (_, _, item, _) = await AddInventoryAsync(ctx, quantityOnHand: 50, averageCost: 30);
        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer());

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Cash,
            Lines = { new SalesInvoiceLineInputDto { Description = "بيع منتج", Quantity = 1, UnitPrice = 100, ItemId = item.Id } }
        }));
    }
}
