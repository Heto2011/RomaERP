using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Purchasing;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class PurchaseNoteTests
{
    private record Rig(PurchaseNoteService Svc, ApplicationDbContext C, PurchaseInvoice Inv, Account Stock, FiscalPeriod Period, Vendor Vendor);

    private static async Task<Rig> BuildAsync(string currency = "EGP", bool closed = false)
    {
        var c = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var ap = new Account { Code = AccountingConstants.AccountsPayableAccountCode, NameAr = "الموردون", NameEn = "AP", IsActive = true };
        var vat = new Account { Code = AccountingConstants.InputVatAccountCode, NameAr = "ضريبة مدخلات", NameEn = "Input VAT", IsActive = true };
        var stock = new Account { Code = "1160", NameAr = "مخزون", NameEn = "Stock", IsActive = true };
        c.Accounts.AddRange(ap, vat, stock);
        var year = new FiscalYear { Name = "Y", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "P", PeriodNumber = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsClosed = closed };
        c.FiscalYears.Add(year); c.FiscalPeriods.Add(period);
        var vendor = new Vendor { Code = "V1", NameAr = "النور", NameEn = "Noor", ApBalance = 1140 };
        c.Vendors.Add(vendor);
        var inv = new PurchaseInvoice { InvoiceNumber = "PI-1", InvoiceDate = new DateTime(2026, 3, 1), Vendor = vendor, VendorId = vendor.Id, FiscalPeriodId = period.Id, CurrencyCode = currency, TotalAmount = 1140 };
        c.PurchaseInvoices.Add(inv);
        c.CompanySettings.Add(new RomaERP.Domain.Tenancy.CompanySettings { VatRate = 0.14m, DefaultCurrency = "EGP" });
        await c.SaveChangesAsync();
        return new Rig(new PurchaseNoteService(c), c, inv, stock, period, vendor);
    }

    private static CreatePurchaseNoteDto Dto(Rig r, PurchaseNoteType t) => new()
    {
        OriginalInvoiceId = r.Inv.Id, NoteType = t, NoteDate = new DateTime(2026, 3, 10), FiscalPeriodId = r.Period.Id, Reason = "بضاعة تالفة",
        Lines = { new PurchaseNoteLineInputDto { Description = "دقيق تالف", Quantity = 2, UnitPrice = 250, AccountId = r.Stock.Id } },
    };

    [Fact]
    public async Task Return_ReducesWhatWeOwe_AndPostsABalancedEntry_DebitingTheSupplier()
    {
        var r = await BuildAsync();
        var n = await r.Svc.CreateAsync(Dto(r, PurchaseNoteType.Return));
        Assert.Equal("PR-000001", n.NoteNumber); Assert.Equal(500m, n.SubTotal); Assert.Equal(70m, n.VatAmount); Assert.Equal(570m, n.TotalAmount);
        Assert.Equal(570m, (await r.C.Vendors.FirstAsync()).ApBalance);
        var e = await r.C.JournalEntries.Include(x => x.Lines).SingleAsync();
        Assert.Equal(e.Lines.Sum(l => l.Debit), e.Lines.Sum(l => l.Credit));
        var ap = await r.C.Accounts.FirstAsync(a => a.Code == AccountingConstants.AccountsPayableAccountCode);
        Assert.Equal(570m, e.Lines.Single(l => l.AccountId == ap.Id).Debit);
        Assert.Equal(500m, e.Lines.Single(l => l.AccountId == r.Stock.Id).Credit);
        Assert.Equal(AccountingConstants.PurchaseReturnNoteReference, e.Reference);
    }

    [Fact]
    public async Task ExtraCharge_IncreasesWhatWeOwe_AndCreditsTheSupplier()
    {
        var r = await BuildAsync();
        var n = await r.Svc.CreateAsync(Dto(r, PurchaseNoteType.AdditionalCharge));
        Assert.Equal("PC-000001", n.NoteNumber);
        Assert.Equal(1710m, (await r.C.Vendors.FirstAsync()).ApBalance);
        var e = await r.C.JournalEntries.Include(x => x.Lines).SingleAsync();
        Assert.Equal(e.Lines.Sum(l => l.Debit), e.Lines.Sum(l => l.Credit));
        var ap = await r.C.Accounts.FirstAsync(a => a.Code == AccountingConstants.AccountsPayableAccountCode);
        Assert.Equal(570m, e.Lines.Single(l => l.AccountId == ap.Id).Credit);
    }

    [Fact]
    public async Task Refuses_AClosedPeriod_AForeignCurrencyInvoice_MissingReason_AndBadLines()
    {
        var closed = await BuildAsync(closed: true);
        await Assert.ThrowsAsync<ValidationAppException>(() => closed.Svc.CreateAsync(Dto(closed, PurchaseNoteType.Return)));
        var usd = await BuildAsync("USD");
        await Assert.ThrowsAsync<ValidationAppException>(() => usd.Svc.CreateAsync(Dto(usd, PurchaseNoteType.Return)));
        var r = await BuildAsync();
        var d = Dto(r, PurchaseNoteType.Return); d.Reason = " ";
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateAsync(d));
        var e = Dto(r, PurchaseNoteType.Return); e.Lines[0].Quantity = 0;
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateAsync(e));
        var f = Dto(r, PurchaseNoteType.Return); f.Lines[0].AccountId = Guid.NewGuid();
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateAsync(f));
        Assert.Empty(await r.C.JournalEntries.ToListAsync());
        Assert.Equal(1140m, (await r.C.Vendors.FirstAsync()).ApBalance);
    }
}
