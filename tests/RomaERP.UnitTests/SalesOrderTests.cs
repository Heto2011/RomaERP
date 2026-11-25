using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class SalesOrderTests
{
    private record Rig(SalesOrderService Svc, ApplicationDbContext C, AssistantActionServiceTests.FakeSales Fake, Customer Cust);
    private static async Task<Rig> BuildAsync()
    {
        var c = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cust = new Customer { Code = "C1", NameAr = "منى", NameEn = "Mona", Mobile = "0100" };
        c.Customers.Add(cust); await c.SaveChangesAsync();
        var proxy = DispatchProxy.Create<ISalesService, AssistantActionServiceTests.FakeSales>();
        return new Rig(new SalesOrderService(c, proxy), c, (AssistantActionServiceTests.FakeSales)(object)proxy, cust);
    }
    private static SaveSalesOrderDto O(Guid cust) => new() { CustomerId = cust, OrderDate = new DateTime(2026, 3, 1), DeliveryDate = new DateTime(2026, 3, 5), DeliveryAddress = "القاهرة", Lines = { new QuotationLineInputDto { Description = "كرسي", Quantity = 4, UnitPrice = 250, VatRate = 0.14m } } };

    [Fact]
    public async Task Order_IsNumbered_WorksOutTotals_AndPostsNothing()
    {
        var r = await BuildAsync();
        var o = await r.Svc.CreateAsync(O(r.Cust.Id));
        Assert.Equal("SO-000001", o.OrderNumber); Assert.Equal(1000m, o.SubTotal); Assert.Equal(1140m, o.TotalAmount); Assert.Equal("0100", o.CustomerPhone);
        Assert.Empty(await r.C.JournalEntries.ToListAsync()); Assert.Null(r.Fake.Created);
    }

    [Fact]
    public async Task Flow_ConfirmThenDeliveredThenInvoice_OnlyInOrder_AndTheInvoiceIsMadeOnce()
    {
        var r = await BuildAsync();
        var o = await r.Svc.CreateAsync(O(r.Cust.Id));
        var conv = new ConvertSalesOrderDto { InvoiceDate = new DateTime(2026, 3, 6), FiscalPeriodId = Guid.NewGuid(), PaymentTerm = PaymentTerm.Credit };
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.MarkDeliveredAsync(o.Id, "x"));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ConvertAsync(o.Id, conv));
        await r.Svc.ConfirmAsync(o.Id);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.UpdateAsync(o.Id, O(r.Cust.Id)));
        var d = await r.Svc.MarkDeliveredAsync(o.Id, "أحمد");
        Assert.Equal(SalesOrderStatus.Delivered, d.Status); Assert.Equal("أحمد", d.DeliveredBy);
        var inv = await r.Svc.ConvertAsync(o.Id, conv);
        Assert.Equal(SalesOrderStatus.Invoiced, inv.Status);
        Assert.Equal(250m, r.Fake.Created!.Lines[0].UnitPrice); Assert.Equal(0.14m, r.Fake.Created.Lines[0].VatRate);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ConvertAsync(o.Id, conv));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CancelAsync(o.Id));
    }

    [Fact]
    public async Task Draft_CanBeEdited_AndCancelled_ACancelledOneCannotBeInvoiced()
    {
        var r = await BuildAsync();
        var o = await r.Svc.CreateAsync(O(r.Cust.Id));
        var dto = O(r.Cust.Id); dto.Lines.Clear(); dto.Lines.Add(new QuotationLineInputDto { Description = "طاولة", Quantity = 1, UnitPrice = 900, VatRate = 0 });
        var up = await r.Svc.UpdateAsync(o.Id, dto);
        Assert.Single(up.Lines); Assert.Equal(900m, up.TotalAmount);
        await r.Svc.CancelAsync(o.Id);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ConfirmAsync(o.Id));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ConvertAsync(o.Id, new ConvertSalesOrderDto()));
    }

    [Fact]
    public async Task FromQuotation_CopiesTheLines_OnlyOncePerQuotation_NotFromARejectedOne()
    {
        var r = await BuildAsync();
        var q = new SalesQuotation { QuotationNumber = "QT-1", QuotationDate = new DateTime(2026, 3, 1), CustomerId = r.Cust.Id, Status = QuotationStatus.Accepted };
        q.Lines.Add(new SalesQuotationLine { LineNumber = 1, Description = "كرسي", Quantity = 2, UnitPrice = 300, LineTotal = 600, VatRate = 0, VatAmount = 0 });
        var rejected = new SalesQuotation { QuotationNumber = "QT-2", QuotationDate = new DateTime(2026, 3, 1), CustomerId = r.Cust.Id, Status = QuotationStatus.Rejected };
        rejected.Lines.Add(new SalesQuotationLine { LineNumber = 1, Description = "x", Quantity = 1, UnitPrice = 1, LineTotal = 1 });
        r.C.SalesQuotations.AddRange(q, rejected); await r.C.SaveChangesAsync();
        var o = await r.Svc.CreateFromQuotationAsync(q.Id);
        Assert.Equal(q.Id, o.QuotationId); Assert.Equal(600m, o.SubTotal); Assert.Contains("QT-1", o.Notes);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateFromQuotationAsync(q.Id));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateFromQuotationAsync(rejected.Id));
    }

    [Fact]
    public async Task Save_RefusesBadInput()
    {
        var r = await BuildAsync();
        var a = O(r.Cust.Id); a.Lines.Clear();
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateAsync(a));
        var b = O(r.Cust.Id); b.DeliveryDate = new DateTime(2026, 2, 1);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.CreateAsync(b));
        await Assert.ThrowsAsync<NotFoundException>(() => r.Svc.CreateAsync(O(Guid.NewGuid())));
    }
}
