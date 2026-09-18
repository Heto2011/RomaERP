using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ExchangeRateServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var ctx = CreateContext();
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = 0.14m, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task ResolveAsync_NullCurrency_ReturnsFunctionalCurrencyAtRateOne()
    {
        var ctx = await SeedAsync();
        var service = new ExchangeRateService(ctx, new FakeExchangeRateProvider());

        var (code, rate) = await service.ResolveAsync(null, DateTime.UtcNow);

        Assert.Equal("EGP", code);
        Assert.Equal(1m, rate);
    }

    [Fact]
    public async Task ResolveAsync_ForeignCurrencyWithNoRate_Throws()
    {
        var ctx = await SeedAsync();
        var service = new ExchangeRateService(ctx, new FakeExchangeRateProvider());

        await Assert.ThrowsAsync<ValidationAppException>(() => service.ResolveAsync("USD", DateTime.UtcNow));
    }

    [Fact]
    public async Task SetRateAsync_ThenResolve_UsesLatestRateOnOrBeforeDate()
    {
        var ctx = await SeedAsync();
        var service = new ExchangeRateService(ctx, new FakeExchangeRateProvider());

        await service.SetRateAsync(new SetExchangeRateDto { CurrencyCode = "usd", RateDate = new DateTime(2026, 1, 1), RateToFunctional = 30m });
        await service.SetRateAsync(new SetExchangeRateDto { CurrencyCode = "USD", RateDate = new DateTime(2026, 2, 1), RateToFunctional = 31m });

        var (code, rate) = await service.ResolveAsync("USD", new DateTime(2026, 1, 15));

        Assert.Equal("USD", code);
        Assert.Equal(30m, rate);
    }

    [Fact]
    public async Task SetRateAsync_ForFunctionalCurrency_Throws()
    {
        var ctx = await SeedAsync();
        var service = new ExchangeRateService(ctx, new FakeExchangeRateProvider());

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.SetRateAsync(new SetExchangeRateDto { CurrencyCode = "EGP", RateDate = DateTime.UtcNow, RateToFunctional = 1m }));
    }

    [Fact]
    public async Task AddTrackedCurrencyAsync_FetchesLiveRateAndStoresAsAuto()
    {
        var ctx = await SeedAsync();
        var provider = new StubExchangeRateProvider();
        provider.Rates["USD"] = 30.5m;
        var service = new ExchangeRateService(ctx, provider);

        var result = await service.AddTrackedCurrencyAsync("usd");

        Assert.Equal("USD", result.CurrencyCode);
        Assert.Equal(30.5m, result.RateToFunctional);
        Assert.Equal("Auto", result.Source);
    }

    [Fact]
    public async Task AddTrackedCurrencyAsync_ProviderHasNoRate_Throws()
    {
        var ctx = await SeedAsync();
        var service = new ExchangeRateService(ctx, new StubExchangeRateProvider());

        await Assert.ThrowsAsync<ValidationAppException>(() => service.AddTrackedCurrencyAsync("XYZ"));
    }

    [Fact]
    public async Task RefreshAllTrackedCurrenciesAsync_UpdatesAutoRates_ButNeverClobbersATodayManualOverride()
    {
        var ctx = await SeedAsync();
        var provider = new StubExchangeRateProvider();
        provider.Rates["USD"] = 30m;
        provider.Rates["EUR"] = 33m;
        var service = new ExchangeRateService(ctx, provider);

        await service.AddTrackedCurrencyAsync("USD");
        await service.SetRateAsync(new SetExchangeRateDto { CurrencyCode = "EUR", RateDate = DateTime.UtcNow.Date, RateToFunctional = 40m });

        // The market moves after both were recorded for today.
        provider.Rates["USD"] = 31m;
        provider.Rates["EUR"] = 35m;

        await service.RefreshAllTrackedCurrenciesAsync();

        var (_, usdRate) = await service.ResolveAsync("USD", DateTime.UtcNow);
        var (_, eurRate) = await service.ResolveAsync("EUR", DateTime.UtcNow);

        Assert.Equal(31m, usdRate);
        Assert.Equal(40m, eurRate);
    }

    private class StubExchangeRateProvider : IExchangeRateProvider
    {
        public Dictionary<string, decimal> Rates { get; } = new();

        public Task<decimal?> GetRateAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken ct = default)
            => Task.FromResult(Rates.TryGetValue(fromCurrencyCode, out var rate) ? rate : (decimal?)null);
    }
}

public class SalesServiceMultiCurrencyTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateInvoice_InForeignCurrency_PostsFunctionalAmountsThenRecognizesFxGainOnCollection()
    {
        var ctx = CreateContext();

        var bank = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var ar = new Account { Code = "1120", NameAr = "العملاء", NameEn = "AR", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var fxGain = new Account { Code = "4210", NameAr = "أرباح فروق العملة", NameEn = "FX Gain", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var fxLoss = new Account { Code = "5450", NameAr = "خسائر فروق العملة", NameEn = "FX Loss", AccountType = AccountType.Expense, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-30), EndDate = today.AddDays(30) };
        var customer = new Customer { Code = "CUST-1", NameAr = "عميل تجريبي", NameEn = "Test Customer" };

        ctx.Accounts.AddRange(bank, ar, revenue, fxGain, fxLoss);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        ctx.Customers.Add(customer);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = 0m, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        var exchangeRateService = new ExchangeRateService(ctx, new FakeExchangeRateProvider());
        await exchangeRateService.SetRateAsync(new SetExchangeRateDto { CurrencyCode = "USD", RateDate = today, RateToFunctional = 30m });

        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer(), exchangeRateService);

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = today,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            CurrencyCode = "usd",
            Lines = { new SalesInvoiceLineInputDto { Description = "خدمة استشارية", Quantity = 1, UnitPrice = 100m } }
        });

        // The invoice document itself stays in the transaction currency...
        Assert.Equal("USD", invoice.CurrencyCode);
        Assert.Equal(100m, invoice.TotalAmount);

        // ...but AR and the GL are booked in the functional currency at the invoice-date rate (100 * 30).
        var customerAfterInvoice = await ctx.Customers.FindAsync(customer.Id);
        Assert.Equal(3000m, customerAfterInvoice!.ArBalance);

        var invoiceEntry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Equal(3000m, invoiceEntry.Lines.Single(l => l.AccountId == ar.Id).Debit);
        Assert.Equal(3000m, invoiceEntry.Lines.Single(l => l.AccountId == revenue.Id).Credit);

        // The rate improves by the time the customer actually pays -> a realized FX gain.
        await exchangeRateService.SetRateAsync(new SetExchangeRateDto { CurrencyCode = "USD", RateDate = today.AddDays(5), RateToFunctional = 31m });

        var paid = await service.RecordPaymentAsync(invoice.Id, new RecordSalesPaymentDto
        {
            Amount = 100m,
            Method = PaymentTerm.Card,
            PaymentDate = today.AddDays(5)
        });

        Assert.Equal(0m, paid.OutstandingAmount);
        var customerAfterPayment = await ctx.Customers.FindAsync(customer.Id);
        Assert.Equal(0m, customerAfterPayment!.ArBalance);

        var paymentJournalEntryId = paid.Payments.Single().JournalEntryId!.Value;
        var paymentEntry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == paymentJournalEntryId);

        Assert.Equal(3100m, paymentEntry.Lines.Single(l => l.AccountId == bank.Id).Debit);
        Assert.Equal(3000m, paymentEntry.Lines.Single(l => l.AccountId == ar.Id).Credit);
        Assert.Equal(100m, paymentEntry.Lines.Single(l => l.AccountId == fxGain.Id).Credit);
        Assert.True(invoiceEntry.IsBalanced);
        Assert.True(paymentEntry.IsBalanced);
    }

    [Fact]
    public async Task CreateInvoice_DomesticCurrency_BehavesExactlyAsBeforeMultiCurrency()
    {
        var ctx = CreateContext();

        var bank = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };
        var customer = new Customer { Code = "CUST-1", NameAr = "عميل تجريبي", NameEn = "Test Customer" };

        ctx.Accounts.AddRange(bank, revenue);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        ctx.Customers.Add(customer);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = 0m, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        var service = new SalesService(ctx, new FakeHtmlToPdfRenderer(), new ExchangeRateService(ctx, new FakeExchangeRateProvider()));

        var invoice = await service.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = today,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Card,
            Lines = { new SalesInvoiceLineInputDto { Description = "خدمة", Quantity = 1, UnitPrice = 200m } }
        });

        Assert.Equal("EGP", invoice.CurrencyCode);
        Assert.Equal(1m, invoice.ExchangeRateToFunctional);
        Assert.Equal(200m, invoice.TotalAmount);
    }
}

public class PurchasingServiceMultiCurrencyTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateInvoice_InForeignCurrency_ThenPayAtWorseRate_RecognizesFxLoss()
    {
        var ctx = CreateContext();

        var bank = new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var ap = new Account { Code = "2120", NameAr = "الموردون", NameEn = "AP", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var expense = new Account { Code = "5300", NameAr = "مصروفات إدارية", NameEn = "Admin Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var fxGain = new Account { Code = "4210", NameAr = "أرباح فروق العملة", NameEn = "FX Gain", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var fxLoss = new Account { Code = "5450", NameAr = "خسائر فروق العملة", NameEn = "FX Loss", AccountType = AccountType.Expense, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-30), EndDate = today.AddDays(30) };
        var vendor = new Vendor { Code = "VEND-1", NameAr = "مورد تجريبي", NameEn = "Test Vendor" };

        ctx.Accounts.AddRange(bank, ap, expense, fxGain, fxLoss);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        ctx.Vendors.Add(vendor);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = 0m, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        var exchangeRateService = new ExchangeRateService(ctx, new FakeExchangeRateProvider());
        await exchangeRateService.SetRateAsync(new SetExchangeRateDto { CurrencyCode = "USD", RateDate = today, RateToFunctional = 30m });

        var service = new PurchasingService(ctx, new FakeHtmlToPdfRenderer(), new ItemLotService(ctx), exchangeRateService);

        var invoice = await service.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
        {
            VendorId = vendor.Id,
            InvoiceDate = today,
            FiscalPeriodId = period.Id,
            PaymentTerm = PaymentTerm.Credit,
            CurrencyCode = "USD",
            Lines = { new PurchaseInvoiceLineInputDto { Description = "استشارات", AccountId = expense.Id, Quantity = 1, UnitPrice = 100m } }
        });

        var vendorAfterInvoice = await ctx.Vendors.FindAsync(vendor.Id);
        Assert.Equal(3000m, vendorAfterInvoice!.ApBalance);

        // The rate worsens by the time we actually pay -> a realized FX loss.
        await exchangeRateService.SetRateAsync(new SetExchangeRateDto { CurrencyCode = "USD", RateDate = today.AddDays(5), RateToFunctional = 31m });

        var paid = await service.RecordPaymentAsync(invoice.Id, new RecordPurchasePaymentDto
        {
            Amount = 100m,
            Method = PaymentTerm.Card,
            PaymentDate = today.AddDays(5)
        });

        Assert.Equal(0m, paid.OutstandingAmount);
        var vendorAfterPayment = await ctx.Vendors.FindAsync(vendor.Id);
        Assert.Equal(0m, vendorAfterPayment!.ApBalance);

        var paymentJournalEntryId = paid.Payments.Single().JournalEntryId!.Value;
        var paymentEntry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == paymentJournalEntryId);

        Assert.Equal(3000m, paymentEntry.Lines.Single(l => l.AccountId == ap.Id).Debit);
        Assert.Equal(3100m, paymentEntry.Lines.Single(l => l.AccountId == bank.Id).Credit);
        Assert.Equal(100m, paymentEntry.Lines.Single(l => l.AccountId == fxLoss.Id).Debit);
        Assert.True(paymentEntry.IsBalanced);
    }
}
