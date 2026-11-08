using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class LedgerReportServiceTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static JournalEntry Entry(string number, DateTime date, JournalEntryStatus status, params JournalEntryLine[] lines)
    {
        var entry = new JournalEntry { EntryNumber = number, EntryDate = date, FiscalPeriodId = Guid.NewGuid(), Status = status };
        var n = 1;
        foreach (var l in lines) { l.LineNumber = n++; entry.Lines.Add(l); }
        return entry;
    }

    [Fact]
    public async Task GeneralLedger_carries_the_opening_balance_and_keeps_a_running_balance()
    {
        var ctx = CreateContext();
        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var sales = new Account { Code = "4100", NameAr = "المبيعات", NameEn = "Sales", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        ctx.Accounts.AddRange(cash, sales);
        await ctx.SaveChangesAsync();

        ctx.JournalEntries.AddRange(
            Entry("JV-1", new DateTime(2026, 9, 10), JournalEntryStatus.Posted,
                new JournalEntryLine { AccountId = cash.Id, Debit = 1000 }, new JournalEntryLine { AccountId = sales.Id, Credit = 1000 }),
            Entry("JV-2", new DateTime(2026, 10, 5), JournalEntryStatus.Posted,
                new JournalEntryLine { AccountId = cash.Id, Debit = 300 }, new JournalEntryLine { AccountId = sales.Id, Credit = 300 }),
            Entry("JV-3", new DateTime(2026, 10, 20), JournalEntryStatus.Posted,
                new JournalEntryLine { AccountId = cash.Id, Credit = 100 }, new JournalEntryLine { AccountId = sales.Id, Debit = 100 }),
            // A draft and a date after the period never count.
            Entry("JV-4", new DateTime(2026, 10, 6), JournalEntryStatus.Draft, new JournalEntryLine { AccountId = cash.Id, Debit = 9999 }),
            Entry("JV-5", new DateTime(2026, 11, 2), JournalEntryStatus.Posted, new JournalEntryLine { AccountId = cash.Id, Debit = 7777 }));
        await ctx.SaveChangesAsync();

        var svc = new LedgerReportService(ctx);
        var cashLedger = await svc.GetGeneralLedgerAsync(cash.Id, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

        Assert.Equal(1000, cashLedger.OpeningBalance);
        Assert.Equal(new[] { "JV-2", "JV-3" }, cashLedger.Lines.Select(l => l.EntryNumber));
        Assert.Equal(new[] { 1300m, 1200m }, cashLedger.Lines.Select(l => l.Balance));
        Assert.Equal(300, cashLedger.TotalDebit);
        Assert.Equal(100, cashLedger.TotalCredit);
        Assert.Equal(1200, cashLedger.ClosingBalance);

        // A credit-nature account grows with credits, so its balance reads positive.
        var salesLedger = await svc.GetGeneralLedgerAsync(sales.Id, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        Assert.Equal(1000, salesLedger.OpeningBalance);
        Assert.Equal(1200, salesLedger.ClosingBalance);
    }

    [Fact]
    public async Task GeneralLedger_rejects_an_unknown_account_and_a_reversed_range()
    {
        var svc = new LedgerReportService(CreateContext());
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetGeneralLedgerAsync(Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 2, 1)));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.GetGeneralLedgerAsync(Guid.NewGuid(), new DateTime(2026, 2, 1), new DateTime(2026, 1, 1)));
    }

    [Fact]
    public async Task CustomerStatement_follows_the_receivable_balance_through_invoices_payments_and_notes()
    {
        var ctx = CreateContext();
        var customer = new Customer { Code = "C-1", NameAr = "عميل" };
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        var credit = new SalesInvoice { InvoiceNumber = "INV-1", InvoiceDate = new DateTime(2026, 9, 1), CustomerId = customer.Id, TotalAmount = 1000, ExchangeRateToFunctional = 1, PaymentTerm = PaymentTerm.Credit, FiscalPeriodId = Guid.NewGuid() };
        var cash = new SalesInvoice { InvoiceNumber = "INV-2", InvoiceDate = new DateTime(2026, 10, 3), CustomerId = customer.Id, TotalAmount = 500, ExchangeRateToFunctional = 1, PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = Guid.NewGuid() };
        var second = new SalesInvoice { InvoiceNumber = "INV-3", InvoiceDate = new DateTime(2026, 10, 10), CustomerId = customer.Id, TotalAmount = 400, ExchangeRateToFunctional = 1, PaymentTerm = PaymentTerm.Installment, FiscalPeriodId = Guid.NewGuid() };
        ctx.SalesInvoices.AddRange(credit, cash, second);
        await ctx.SaveChangesAsync();

        ctx.SalesPayments.Add(new SalesPayment { SalesInvoiceId = credit.Id, PaymentDate = new DateTime(2026, 10, 15), Amount = 600, Method = PaymentTerm.Cash });
        ctx.SalesNotes.Add(new SalesNote { NoteNumber = "CN-1", NoteType = SalesNoteType.Credit, NoteDate = new DateTime(2026, 10, 20), OriginalInvoiceId = credit.Id, CustomerId = customer.Id, TotalAmount = 100, Reason = "مرتجع", FiscalPeriodId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();

        var statement = await new LedgerReportService(ctx).GetCustomerStatementAsync(customer.Id, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

        Assert.True(statement.IsCustomer);
        Assert.Equal(1000, statement.OpeningBalance);
        // The cash sale never touches receivable; the rest do.
        Assert.Equal(new[] { "INV-3", "INV-1", "CN-1" }, statement.Lines.Select(l => l.Number));
        Assert.Equal(new[] { 1400m, 800m, 700m }, statement.Lines.Select(l => l.Balance));
        Assert.Equal(700, statement.ClosingBalance);
        Assert.Equal(400, statement.TotalDebit);
        Assert.Equal(700, statement.TotalCredit);
    }

    [Fact]
    public async Task VendorStatement_follows_the_payable_balance_and_ignores_cash_purchases()
    {
        var ctx = CreateContext();
        var vendor = new Vendor { Code = "V-1", NameAr = "مورد" };
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        var credit = new PurchaseInvoice { InvoiceNumber = "PUR-1", InvoiceDate = new DateTime(2026, 10, 2), VendorId = vendor.Id, TotalAmount = 800, ExchangeRateToFunctional = 1, PaymentTerm = PaymentTerm.Credit, FiscalPeriodId = Guid.NewGuid() };
        var cash = new PurchaseInvoice { InvoiceNumber = "PUR-2", InvoiceDate = new DateTime(2026, 10, 3), VendorId = vendor.Id, TotalAmount = 50, ExchangeRateToFunctional = 1, PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = Guid.NewGuid() };
        ctx.PurchaseInvoices.AddRange(credit, cash);
        await ctx.SaveChangesAsync();
        ctx.PurchasePayments.Add(new PurchasePayment { PurchaseInvoiceId = credit.Id, PaymentDate = new DateTime(2026, 10, 9), Amount = 300, Method = PaymentTerm.Cash });
        await ctx.SaveChangesAsync();

        var statement = await new LedgerReportService(ctx).GetVendorStatementAsync(vendor.Id, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

        Assert.False(statement.IsCustomer);
        Assert.Equal(0, statement.OpeningBalance);
        Assert.Equal(new[] { "PUR-1", "PUR-1" }, statement.Lines.Select(l => l.Number));
        Assert.Equal(new[] { PartnerStatementLineType.Invoice, PartnerStatementLineType.Payment }, statement.Lines.Select(l => l.Type));
        Assert.Equal(new[] { 800m, 500m }, statement.Lines.Select(l => l.Balance));
        Assert.Equal(500, statement.ClosingBalance);
    }
}
