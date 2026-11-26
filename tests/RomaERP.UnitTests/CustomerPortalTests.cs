using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class CustomerPortalTests
{
    public class FakeLedger : DispatchProxy
    {
        public Guid? AskedFor { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetCustomerStatementAsync") { AskedFor = (Guid)args![0]!; return Task.FromResult(new PartnerStatementDto { ClosingBalance = 1234m }); }
            throw new NotSupportedException(method.Name);
        }
    }
    public class FakeSalesPdf : DispatchProxy
    {
        public Guid? PdfFor { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetInvoicePdfAsync") { PdfFor = (Guid)args![0]!; return Task.FromResult(new byte[] { 1, 2, 3 }); }
            throw new NotSupportedException(method.Name);
        }
    }

    private record Rig(CustomerPortalService Svc, ApplicationDbContext C, Customer Mona, Customer Other, FakeLedger Ledger, FakeSalesPdf Sales);

    private static async Task<Rig> BuildAsync()
    {
        var c = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var mona = new Customer { Code = "C1", NameAr = "منى", NameEn = "Mona" }; var other = new Customer { Code = "C2", NameAr = "كريم", NameEn = "Karim" };
        c.Customers.AddRange(mona, other);
        c.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة النور", CompanyNameEn = "Noor", DefaultCurrency = "EGP" });
        c.SalesInvoices.Add(new SalesInvoice { InvoiceNumber = "SI-1", InvoiceDate = new DateTime(2026, 3, 1), CustomerId = mona.Id, TotalAmount = 1000, PaidAmount = 400 });
        c.SalesInvoices.Add(new SalesInvoice { InvoiceNumber = "SI-2", InvoiceDate = new DateTime(2026, 3, 2), CustomerId = other.Id, TotalAmount = 777 });
        await c.SaveChangesAsync();
        var ledger = DispatchProxy.Create<ILedgerReportService, FakeLedger>(); var sales = DispatchProxy.Create<ISalesService, FakeSalesPdf>();
        return new Rig(new CustomerPortalService(c, ledger, sales), c, mona, other, (FakeLedger)(object)ledger, (FakeSalesPdf)(object)sales);
    }

    [Fact]
    public async Task TheLink_ShowsOnlyThatCustomersInvoices_AndOnlyAHashOfTheSecretIsStored()
    {
        var r = await BuildAsync();
        var link = await r.Svc.CreateLinkAsync(r.Mona.Id, null);
        Assert.True(link.Token.Length >= 40);
        var stored = await r.C.CustomerPortalLinks.SingleAsync();
        Assert.NotEqual(link.Token, stored.TokenHash); Assert.Equal(CustomerPortalService.Hash(link.Token), stored.TokenHash);
        var p = await r.Svc.GetPortalAsync(link.Token);
        Assert.Equal("منى", p.CustomerName); Assert.Equal("شركة النور", p.CompanyName); Assert.Equal(1234m, p.Balance);
        var inv = Assert.Single(p.Invoices);
        Assert.Equal("SI-1", inv.InvoiceNumber); Assert.Equal(600m, inv.OutstandingAmount);
        Assert.Equal(r.Mona.Id, r.Ledger.AskedFor);
    }

    [Fact]
    public async Task ABadWrongExpiredOrSwitchedOffLink_AllLookTheSame_AndAreRefused()
    {
        var r = await BuildAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.GetPortalAsync(""));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.GetPortalAsync("short"));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.GetPortalAsync(new string('x', 43)));
        var link = await r.Svc.CreateLinkAsync(r.Mona.Id, 30);
        var stored = await r.C.CustomerPortalLinks.SingleAsync();
        stored.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1); await r.C.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.GetPortalAsync(link.Token));
        var fresh = await r.Svc.CreateLinkAsync(r.Mona.Id, 30);
        await r.Svc.RevokeAsync(r.Mona.Id);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.GetPortalAsync(fresh.Token));
        Assert.False((await r.Svc.GetStatusAsync(r.Mona.Id)).HasActiveLink);
    }

    [Fact]
    public async Task ANewLink_SwitchesOffTheOldOne_AndViewsAreCounted()
    {
        var r = await BuildAsync();
        var a = await r.Svc.CreateLinkAsync(r.Mona.Id, null);
        var b = await r.Svc.CreateLinkAsync(r.Mona.Id, null);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.GetPortalAsync(a.Token));
        await r.Svc.GetPortalAsync(b.Token); await r.Svc.GetPortalAsync(b.Token);
        var st = await r.Svc.GetStatusAsync(r.Mona.Id);
        Assert.True(st.HasActiveLink); Assert.Equal(2, st.ViewCount); Assert.NotNull(st.LastViewedAtUtc);
    }

    [Fact]
    public async Task Pdf_OnlyForTheLinkOwnersOwnInvoices()
    {
        var r = await BuildAsync();
        var link = await r.Svc.CreateLinkAsync(r.Mona.Id, null);
        var mine = (await r.C.SalesInvoices.FirstAsync(i => i.CustomerId == r.Mona.Id)).Id;
        var theirs = (await r.C.SalesInvoices.FirstAsync(i => i.CustomerId == r.Other.Id)).Id;
        Assert.Equal(3, (await r.Svc.GetInvoicePdfAsync(link.Token, mine)).Length);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.GetInvoicePdfAsync(link.Token, theirs));
        Assert.Equal(mine, r.Sales.PdfFor);
    }

    [Fact]
    public async Task CreatingALink_RefusesAnUnknownCustomer_AndABadDuration()
    {
        var r = await BuildAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => r.Svc.CreateLinkAsync(Guid.NewGuid(), null));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateLinkAsync(r.Mona.Id, 0));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateLinkAsync(r.Mona.Id, 400));
    }
}
