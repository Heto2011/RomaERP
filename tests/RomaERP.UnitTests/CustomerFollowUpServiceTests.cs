using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class CustomerFollowUpServiceTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SalesInvoice Invoice(Customer c, string number, DateTime date, decimal total, decimal paid, PaymentTerm term = PaymentTerm.Credit)
        => new() { InvoiceNumber = number, InvoiceDate = date, Customer = c, CustomerId = c.Id, FiscalPeriodId = Guid.NewGuid(), TotalAmount = total, PaidAmount = paid, PaymentTerm = term, CurrencyCode = "EGP" };

    [Fact]
    public async Task Overdue_UsesInvoiceDatePlusCreditDays_AndSkipsTheOnesNotYetDue()
    {
        var ctx = CreateContext();
        var today = new DateTime(2026, 11, 8);
        var c = new Customer { Code = "C1", NameAr = "عميل", NameEn = "Cust", PaymentTermsDays = 30 };
        ctx.Customers.Add(c);
        ctx.SalesInvoices.AddRange(
            Invoice(c, "INV-1", today.AddDays(-45), 1000, 200),   // due 15 days ago, 800 left
            Invoice(c, "INV-2", today.AddDays(-10), 500, 0),      // not due yet
            Invoice(c, "INV-3", today.AddDays(-90), 300, 300),    // fully paid
            Invoice(c, "INV-4", today.AddDays(-5), 100, 0, PaymentTerm.Cash));  // not a credit sale
        await ctx.SaveChangesAsync();

        var result = await new CustomerFollowUpService(ctx).GetOverdueAsync(today);

        var row = Assert.Single(result);
        Assert.Equal(800, row.TotalOverdue);
        Assert.Equal(15, row.OldestDaysOverdue);
        Assert.Equal("INV-1", Assert.Single(row.Invoices).InvoiceNumber);
    }

    [Fact]
    public async Task Overdue_ForInstallments_UsesTheFirstInstallmentNotYetCovered()
    {
        var ctx = CreateContext();
        var today = new DateTime(2026, 11, 8);
        var c = new Customer { Code = "C1", NameAr = "عميل", NameEn = "Cust" };
        var inv = Invoice(c, "INV-1", today.AddDays(-100), 300, 100, PaymentTerm.Installment);
        inv.InstallmentLines.Add(new SalesInstallmentLine { InstallmentNumber = 1, DueDate = today.AddDays(-70), Amount = 100 });
        inv.InstallmentLines.Add(new SalesInstallmentLine { InstallmentNumber = 2, DueDate = today.AddDays(-40), Amount = 100 });
        inv.InstallmentLines.Add(new SalesInstallmentLine { InstallmentNumber = 3, DueDate = today.AddDays(-10), Amount = 100 });
        ctx.Customers.Add(c);
        ctx.SalesInvoices.Add(inv);
        await ctx.SaveChangesAsync();

        var row = Assert.Single(await new CustomerFollowUpService(ctx).GetOverdueAsync(today));

        Assert.Equal(40, row.OldestDaysOverdue);   // installment 1 is paid, installment 2 (40 days ago) is the next one due
        Assert.Equal(200, row.TotalOverdue);
    }

    [Fact]
    public async Task AddFollowUp_IsReturnedAsTheLastFollowUp_AndValidatesInput()
    {
        var ctx = CreateContext();
        var today = new DateTime(2026, 11, 8);
        var c = new Customer { Code = "C1", NameAr = "عميل", NameEn = "Cust" };
        ctx.Customers.Add(c);
        ctx.SalesInvoices.Add(Invoice(c, "INV-1", today.AddDays(-20), 500, 0));
        await ctx.SaveChangesAsync();
        var svc = new CustomerFollowUpService(ctx);

        await svc.AddFollowUpAsync(new CreateFollowUpDto { CustomerId = c.Id, FollowUpDate = today, Method = "مكالمة", Note = "وعد يدفع الأسبوع الجاي", PromisedPayDate = today.AddDays(7) });

        var row = Assert.Single(await svc.GetOverdueAsync(today));
        Assert.Equal("مكالمة", row.LastFollowUp!.Method);
        Assert.Equal(today.AddDays(7), row.LastFollowUp.PromisedPayDate);
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.AddFollowUpAsync(new CreateFollowUpDto { CustomerId = c.Id, Method = " " }));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.AddFollowUpAsync(new CreateFollowUpDto { CustomerId = Guid.NewGuid(), Method = "زيارة" }));
    }

    [Fact]
    public async Task EmailReminder_WithoutAnEmailOrProvider_SaysWhyAndSendsNothing()
    {
        var ctx = CreateContext();
        var c = new Customer { Code = "C1", NameAr = "عميل", NameEn = "Cust" };
        ctx.Customers.Add(c);
        await ctx.SaveChangesAsync();

        var result = await new CustomerFollowUpService(ctx).SendReminderEmailAsync(c.Id);

        Assert.False(result.Sent);
        Assert.Contains("إيميل", result.Message);
        Assert.Equal(0, await ctx.CustomerFollowUps.CountAsync());
    }
}
