using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

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

    [Fact]
    public async Task CreateInvoice_WithCashTerm_SettlesImmediatelyWithoutTouchingAr()
    {
        var (ctx, cash, _, _, revenue, outputVat, customer, period) = await SeedAsync();
        var service = new SalesService(ctx);

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
        var service = new SalesService(ctx);

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
    public async Task RecordPayment_OnCreditInvoice_ReducesOutstandingAndCustomerBalance()
    {
        var (ctx, _, bank, ar, _, _, customer, period) = await SeedAsync();
        var service = new SalesService(ctx);

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
        var service = new SalesService(ctx);

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            Lines = { new SalesInvoiceLineInputDto { Description = "منتج", Quantity = 1, UnitPrice = 100 } }
        });

        await Assert.ThrowsAsync<RomaERP.Application.Common.Exceptions.ValidationAppException>(
            () => service.RecordPaymentAsync(invoice.Id, new RecordSalesPaymentDto { Amount = 1000, Method = PaymentTerm.Cash, PaymentDate = DateTime.UtcNow.Date }));
    }
}
