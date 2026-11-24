using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class QuotationAndOrderTests
{
    private class Who : ICurrentUserService { public string? UserId => "u1"; public string? UserName => "أحمد"; }
    private static ApplicationDbContext Ctx() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (QuotationService Svc, ApplicationDbContext C, AssistantActionServiceTests.FakeSales Fake, Customer Cust) Quote()
    {
        var c = Ctx();
        var cust = new Customer { Code = "C1", NameAr = "منى", NameEn = "Mona" };
        c.Customers.Add(cust); c.SaveChanges();
        var proxy = DispatchProxy.Create<ISalesService, AssistantActionServiceTests.FakeSales>();
        return (new QuotationService(c, proxy), c, (AssistantActionServiceTests.FakeSales)(object)proxy, cust);
    }
    private static SaveQuotationDto Q(Guid cust) => new() { CustomerId = cust, QuotationDate = new DateTime(2026, 3, 1), ValidUntil = new DateTime(2026, 3, 31), Lines = { new QuotationLineInputDto { Description = "اشتراك", Quantity = 2, UnitPrice = 500, VatRate = 0.14m } } };

    [Fact]
    public async Task Quotation_WorksOutTotals_AndIsNumbered_WithNoAccountingPosted()
    {
        var (s, c, _, cust) = Quote();
        var q = await s.CreateAsync(Q(cust.Id));
        Assert.Equal("QT-000001", q.QuotationNumber);
        Assert.Equal(1000m, q.SubTotal); Assert.Equal(140m, q.VatAmount); Assert.Equal(1140m, q.TotalAmount);
        Assert.Equal(QuotationStatus.Draft, q.Status);
        Assert.Equal("QT-000002", (await s.CreateAsync(Q(cust.Id))).QuotationNumber);
        Assert.Empty(await c.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task Quotation_RefusesBadInput()
    {
        var (s, _, _, cust) = Quote();
        var empty = Q(cust.Id); empty.Lines.Clear();
        await Assert.ThrowsAsync<ValidationAppException>(() => s.CreateAsync(empty));
        var neg = Q(cust.Id); neg.Lines[0].UnitPrice = -1;
        await Assert.ThrowsAsync<ValidationAppException>(() => s.CreateAsync(neg));
        var early = Q(cust.Id); early.ValidUntil = new DateTime(2026, 2, 1);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.CreateAsync(early));
        await Assert.ThrowsAsync<NotFoundException>(() => s.CreateAsync(Q(Guid.NewGuid())));
    }

    [Fact]
    public async Task Quotation_StatusMovesOnlyTheRightWay_AndAnAcceptedOneCannotBeEdited()
    {
        var (s, _, _, cust) = Quote();
        var q = await s.CreateAsync(Q(cust.Id));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.SetStatusAsync(q.Id, QuotationStatus.Converted));
        await s.SetStatusAsync(q.Id, QuotationStatus.Sent);
        await s.SetStatusAsync(q.Id, QuotationStatus.Accepted);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.UpdateAsync(q.Id, Q(cust.Id)));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.SetStatusAsync(q.Id, QuotationStatus.Rejected));
    }

    [Fact]
    public async Task Quotation_Convert_GoesThroughTheInvoiceService_AndOnlyOnce()
    {
        var (s, _, fake, cust) = Quote();
        var q = await s.CreateAsync(Q(cust.Id));
        var period = Guid.NewGuid();
        var done = await s.ConvertAsync(q.Id, new ConvertQuotationDto { InvoiceDate = new DateTime(2026, 3, 5), FiscalPeriodId = period, PaymentTerm = RomaERP.Domain.Common.PaymentTerm.Credit });
        Assert.Equal(QuotationStatus.Converted, done.Status);
        Assert.Equal(cust.Id, fake.Created!.CustomerId);
        Assert.Equal(500m, fake.Created.Lines[0].UnitPrice);
        Assert.Equal(0.14m, fake.Created.Lines[0].VatRate);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.ConvertAsync(q.Id, new ConvertQuotationDto { FiscalPeriodId = period }));
    }

    [Fact]
    public async Task Quotation_ARejectedOneCannotBeConverted_UntilReopened()
    {
        var (s, _, _, cust) = Quote();
        var q = await s.CreateAsync(Q(cust.Id));
        await s.SetStatusAsync(q.Id, QuotationStatus.Rejected);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.ConvertAsync(q.Id, new ConvertQuotationDto()));
        await s.SetStatusAsync(q.Id, QuotationStatus.Draft);
        Assert.Equal(QuotationStatus.Draft, (await s.GetAsync(q.Id)).Status);
    }

    private static (PurchaseOrderService Svc, AssistantActionServiceTests.FakePurchasing Fake, Vendor V) Order()
    {
        var c = Ctx();
        var v = new Vendor { Code = "V1", NameAr = "النور", NameEn = "Noor" };
        c.Vendors.Add(v); c.SaveChanges();
        var proxy = DispatchProxy.Create<IPurchasingService, AssistantActionServiceTests.FakePurchasing>();
        return (new PurchaseOrderService(c, proxy, new Who()), (AssistantActionServiceTests.FakePurchasing)(object)proxy, v);
    }
    private static SavePurchaseOrderDto O(Guid vendor) => new() { VendorId = vendor, OrderDate = new DateTime(2026, 3, 1), Lines = { new PurchaseOrderLineInputDto { Description = "دقيق", Quantity = 10, UnitPrice = 20, VatRate = 0 } } };

    [Fact]
    public async Task Order_MustBeApproved_BeforeItBecomesAnInvoice_AndKeepsWhoApproved()
    {
        var (s, fake, v) = Order();
        var o = await s.CreateAsync(O(v.Id));
        Assert.Equal("PO-000001", o.OrderNumber); Assert.Equal(200m, o.TotalAmount);
        var conv = new ConvertPurchaseOrderDto { InvoiceDate = new DateTime(2026, 3, 5), FiscalPeriodId = Guid.NewGuid(), DefaultAccountId = Guid.NewGuid() };
        await Assert.ThrowsAsync<ValidationAppException>(() => s.ConvertAsync(o.Id, conv));
        var ap = await s.ApproveAsync(o.Id);
        Assert.Equal("أحمد", ap.ApprovedBy);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.UpdateAsync(o.Id, O(v.Id)));
        var done = await s.ConvertAsync(o.Id, conv);
        Assert.Equal(PurchaseOrderStatus.Converted, done.Status);
        Assert.Equal(conv.DefaultAccountId, fake.Invoice!.Lines[0].AccountId);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.ConvertAsync(o.Id, conv));
    }

    [Fact]
    public async Task Order_LinesWithoutAnAccount_NeedOneAtConversion_AndCancelledCannotBeConverted()
    {
        var (s, _, v) = Order();
        var o = await s.CreateAsync(O(v.Id));
        await s.ApproveAsync(o.Id);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.ConvertAsync(o.Id, new ConvertPurchaseOrderDto { FiscalPeriodId = Guid.NewGuid() }));
        await s.CancelAsync(o.Id);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.ConvertAsync(o.Id, new ConvertPurchaseOrderDto { DefaultAccountId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.CancelAsync(o.Id));
    }
}
