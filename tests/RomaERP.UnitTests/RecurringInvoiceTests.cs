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

public class RecurringInvoiceTests
{
    private record Rig(RecurringInvoiceService Svc, ApplicationDbContext C, AssistantActionServiceTests.FakeSales Fake, Customer Cust);

    private static async Task<Rig> BuildAsync()
    {
        var c = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cust = new Customer { Code = "C1", NameAr = "منى", NameEn = "Mona" };
        c.Customers.Add(cust); await c.SaveChangesAsync();
        var proxy = DispatchProxy.Create<ISalesService, AssistantActionServiceTests.FakeSales>();
        return new Rig(new RecurringInvoiceService(c, proxy), c, (AssistantActionServiceTests.FakeSales)(object)proxy, cust);
    }
    private static SaveRecurringInvoiceDto Tpl(Guid cust, int interval = 1, int day = 10, DateTime? start = null) => new()
    {
        Title = "اشتراك", CustomerId = cust, IntervalMonths = interval, DayOfMonth = day, StartDate = start ?? new DateTime(2026, 1, 15), PaymentTerm = PaymentTerm.Credit,
        Lines = { new RecurringLineInputDto { Description = "اشتراك شهري", Quantity = 1, UnitPrice = 500 } },
    };

    [Fact]
    public async Task Monthly_ComesDueOnItsDay_OnlyOncePerMonth_AndNothingIsInvoiced()
    {
        var r = await BuildAsync();
        await r.Svc.SaveAsync(null, Tpl(r.Cust.Id));
        Assert.Equal(0, await r.Svc.PrepareDueAsync(new DateTime(2026, 3, 9)));
        Assert.Equal(1, await r.Svc.PrepareDueAsync(new DateTime(2026, 3, 10)));
        Assert.Equal(0, await r.Svc.PrepareDueAsync(new DateTime(2026, 3, 25)));
        Assert.Equal(1, await r.Svc.PrepareDueAsync(new DateTime(2026, 4, 12)));
        Assert.Null(r.Fake.Created);
        Assert.Equal(2, (await r.Svc.GetPendingAsync()).Count);
    }

    [Fact]
    public async Task Quarterly_FollowsItsOwnMonths_EndDateStopsIt_PausedNeverComesDue_AndDay31UsesTheLastDay()
    {
        var r = await BuildAsync();
        await r.Svc.SaveAsync(null, Tpl(r.Cust.Id, interval: 3, day: 1, start: new DateTime(2026, 1, 1)));
        Assert.Equal(0, await r.Svc.PrepareDueAsync(new DateTime(2026, 2, 5)));
        Assert.Equal(1, await r.Svc.PrepareDueAsync(new DateTime(2026, 4, 2)));
        var ended = await BuildAsync();
        var dto = Tpl(ended.Cust.Id, start: new DateTime(2026, 1, 1)); dto.EndDate = new DateTime(2026, 2, 28);
        await ended.Svc.SaveAsync(null, dto);
        Assert.Equal(0, await ended.Svc.PrepareDueAsync(new DateTime(2026, 4, 20)));
        var paused = await BuildAsync();
        var p = await paused.Svc.SaveAsync(null, Tpl(paused.Cust.Id, start: new DateTime(2026, 1, 1)));
        await paused.Svc.SetActiveAsync(p.Id, false);
        Assert.Equal(0, await paused.Svc.PrepareDueAsync(new DateTime(2026, 3, 20)));
        Assert.Equal(new DateTime(2026, 2, 28), RecurringInvoiceService.DueDateIn(2026, 2, 31));
        var last = await BuildAsync();
        await last.Svc.SaveAsync(null, Tpl(last.Cust.Id, day: 31, start: new DateTime(2026, 1, 1)));
        Assert.Equal(0, await last.Svc.PrepareDueAsync(new DateTime(2026, 2, 27)));
        Assert.Equal(1, await last.Svc.PrepareDueAsync(new DateTime(2026, 2, 28)));
    }

    [Fact]
    public async Task Issue_GoesThroughTheInvoiceService_WithTheTemplateOrTheEditedLines_AndOnlyOnce()
    {
        var r = await BuildAsync();
        await r.Svc.SaveAsync(null, Tpl(r.Cust.Id));
        await r.Svc.PrepareDueAsync(new DateTime(2026, 3, 10));
        var due = (await r.Svc.GetPendingAsync()).Single();
        var period = Guid.NewGuid();
        var edited = new IssueRecurringDto { InvoiceDate = new DateTime(2026, 3, 10), FiscalPeriodId = period, Lines = new() { new RecurringLineInputDto { Description = "اشتراك + إضافة", Quantity = 1, UnitPrice = 650 } } };
        var done = await r.Svc.IssueAsync(due.Id, edited, "أحمد");
        Assert.Equal(RecurringDueStatus.Issued, done.Status);
        Assert.Equal(650m, r.Fake.Created!.Lines[0].UnitPrice);
        Assert.Equal(PaymentTerm.Credit, r.Fake.Created.PaymentTerm);
        Assert.Contains("2026-03", r.Fake.Created.Notes);
        Assert.Equal(500m, (await r.Svc.GetAllAsync()).Single().Total);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.IssueAsync(due.Id, edited, "أحمد"));
        Assert.Empty(await r.Svc.GetPendingAsync());
    }

    [Fact]
    public async Task Skip_ClosesThePeriod_AndANewMonthStillComesDue()
    {
        var r = await BuildAsync();
        await r.Svc.SaveAsync(null, Tpl(r.Cust.Id));
        await r.Svc.PrepareDueAsync(new DateTime(2026, 3, 10));
        var due = (await r.Svc.GetPendingAsync()).Single();
        await r.Svc.SkipAsync(due.Id, "أحمد");
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.IssueAsync(due.Id, new IssueRecurringDto { InvoiceDate = DateTime.UtcNow, FiscalPeriodId = Guid.NewGuid() }, "أحمد"));
        Assert.Equal(0, await r.Svc.PrepareDueAsync(new DateTime(2026, 3, 11)));
        Assert.Equal(1, await r.Svc.PrepareDueAsync(new DateTime(2026, 4, 10)));
    }

    [Fact]
    public async Task Save_RefusesBadInput()
    {
        var r = await BuildAsync();
        var a = Tpl(r.Cust.Id); a.Title = " ";
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SaveAsync(null, a));
        var b = Tpl(r.Cust.Id, interval: 2);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SaveAsync(null, b));
        var c = Tpl(r.Cust.Id); c.Lines.Clear();
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SaveAsync(null, c));
        var d = Tpl(r.Cust.Id); d.PaymentTerm = PaymentTerm.Installment;
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SaveAsync(null, d));
        await Assert.ThrowsAsync<NotFoundException>(() => r.Svc.SaveAsync(null, Tpl(Guid.NewGuid())));
    }
}
