using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Assistant;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class AssistantActionServiceTests
{
    private class FakeParser : IClaudeActionParser
    {
        public ActionIntent Intent { get; set; } = new();
        public Task<ActionIntent> ParseAsync(string text, CancellationToken ct = default) => Task.FromResult(Intent);
    }

    /// <summary>Stands in for the sales service and remembers what it was asked to book.</summary>
    public class FakeSales : DispatchProxy
    {
        public CreateSalesInvoiceDto? Created { get; set; }
        public (Guid Id, RecordSalesPaymentDto Dto)? Paid { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "CreateInvoiceAsync") { Created = (CreateSalesInvoiceDto)args![0]!; return Task.FromResult(new SalesInvoiceDto { InvoiceNumber = "INV-1", TotalAmount = 100 }); }
            if (method.Name == "RecordPaymentAsync") { Paid = ((Guid)args![0]!, (RecordSalesPaymentDto)args[1]!); return Task.FromResult(new SalesInvoiceDto { InvoiceNumber = "INV-7" }); }
            throw new NotSupportedException(method.Name);
        }
    }

    private record Rig(AssistantActionService Svc, ApplicationDbContext Ctx, FakeParser Parser, FakeSales Sales);

    private static async Task<Rig> BuildAsync()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cat = new ItemCategory { Code = "C", NameAr = "ت", NameEn = "C" };
        ctx.ItemCategories.Add(cat);
        ctx.Items.Add(new Item { Code = "M1", NameAr = "قهوة", NameEn = "Coffee", UnitOfMeasure = "كوب", ItemCategoryId = cat.Id, IsMenuItem = true, MenuPrice = 40 });
        ctx.Customers.Add(new Customer { Code = "C1", NameAr = "أحمد علي", NameEn = "Ahmed Ali" });
        ctx.Customers.Add(new Customer { Code = "C2", NameAr = "أحمد حسن", NameEn = "Ahmed Hassan" });
        ctx.Customers.Add(new Customer { Code = "C3", NameAr = "منى", NameEn = "Mona" });
        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = "Y", StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "P", PeriodNumber = 1, StartDate = today.AddDays(-10), EndDate = today.AddDays(10) });
        await ctx.SaveChangesAsync();
        var parser = new FakeParser();
        var proxy = DispatchProxy.Create<ISalesService, FakeSales>();
        var fake = (FakeSales)(object)proxy;
        return new Rig(new AssistantActionService(parser, ctx, proxy), ctx, parser, fake);
    }

    [Fact]
    public async Task Invoice_MatchesTheCustomer_AndTakesTheMenuPriceWhenNoneWasSaid()
    {
        var r = await BuildAsync();
        r.Parser.Intent = new ActionIntent { Kind = AssistantActionKind.SalesInvoice, CustomerName = "منى", Lines = { new ActionInvoiceLineDto { Description = "قهوة", Quantity = 3 } } };
        var p = await r.Svc.ProposeAsync("فاتورة لمنى 3 قهوة");
        Assert.Equal("منى", p.CustomerName);
        Assert.NotNull(p.CustomerId);
        Assert.Equal(40m, p.Lines[0].UnitPrice);
        Assert.Empty(p.Warnings);
    }

    [Fact]
    public async Task Invoice_WithTwoSimilarCustomers_AsksWhichOne_AndNeverGuesses()
    {
        var r = await BuildAsync();
        r.Parser.Intent = new ActionIntent { Kind = AssistantActionKind.SalesInvoice, CustomerName = "أحمد", Lines = { new ActionInvoiceLineDto { Description = "خدمة", Quantity = 1, UnitPrice = 100 } } };
        var p = await r.Svc.ProposeAsync("فاتورة لأحمد");
        Assert.Null(p.CustomerId);
        Assert.Equal(2, p.CustomerCandidates.Count);
        Assert.NotEmpty(p.Warnings);
    }

    [Fact]
    public async Task Invoice_UnknownCustomer_IsNotCreated()
    {
        var r = await BuildAsync();
        r.Parser.Intent = new ActionIntent { Kind = AssistantActionKind.SalesInvoice, CustomerName = "شخص غير موجود", Lines = { new ActionInvoiceLineDto { Description = "x", Quantity = 1, UnitPrice = 1 } } };
        var p = await r.Svc.ProposeAsync("فاتورة");
        Assert.Null(p.CustomerId);
        Assert.Empty(p.CustomerCandidates);
        Assert.Equal(3, await r.Ctx.Customers.CountAsync()); // the assistant never creates a customer on its own
    }

    [Fact]
    public async Task NothingIsBooked_UntilExecuteIsCalled()
    {
        var r = await BuildAsync();
        r.Parser.Intent = new ActionIntent { Kind = AssistantActionKind.SalesInvoice, CustomerName = "منى", Lines = { new ActionInvoiceLineDto { Description = "قهوة", Quantity = 1 } } };
        await r.Svc.ProposeAsync("x");
        Assert.Null(r.Sales.Created);
    }

    [Fact]
    public async Task Execute_Invoice_BooksThroughTheSalesService_WithTheReviewedFields()
    {
        var r = await BuildAsync();
        var mona = await r.Ctx.Customers.FirstAsync(c => c.NameAr == "منى");
        var result = await r.Svc.ExecuteAsync(new ExecuteActionRequestDto
        {
            Kind = AssistantActionKind.SalesInvoice, CustomerId = mona.Id, PaymentTerm = 1,
            Lines = { new ActionInvoiceLineDto { Description = "قهوة", Quantity = 3, UnitPrice = 45 } },
        });
        Assert.Contains("INV-1", result.Message);
        Assert.Equal(mona.Id, r.Sales.Created!.CustomerId);
        Assert.Equal(PaymentTerm.Cash, r.Sales.Created.PaymentTerm);
        Assert.Equal(45m, r.Sales.Created.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task Execute_Invoice_RefusesMissingPriceOrCustomer()
    {
        var r = await BuildAsync();
        var mona = await r.Ctx.Customers.FirstAsync(c => c.NameAr == "منى");
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ExecuteAsync(new ExecuteActionRequestDto { Kind = AssistantActionKind.SalesInvoice, Lines = { new ActionInvoiceLineDto { Description = "x", UnitPrice = 1 } } }));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ExecuteAsync(new ExecuteActionRequestDto { Kind = AssistantActionKind.SalesInvoice, CustomerId = mona.Id, Lines = { new ActionInvoiceLineDto { Description = "x" } } }));
        Assert.Null(r.Sales.Created);
    }

    [Fact]
    public async Task Payment_FindsTheOnlyOpenInvoice_AndFillsTheOutstandingAmount()
    {
        var r = await BuildAsync();
        var mona = await r.Ctx.Customers.FirstAsync(c => c.NameAr == "منى");
        var period = await r.Ctx.FiscalPeriods.FirstAsync();
        r.Ctx.SalesInvoices.Add(new SalesInvoice { InvoiceNumber = "INV-000007", CustomerId = mona.Id, FiscalPeriodId = period.Id, InvoiceDate = DateTime.UtcNow, TotalAmount = 500, PaidAmount = 200 });
        await r.Ctx.SaveChangesAsync();
        r.Parser.Intent = new ActionIntent { Kind = AssistantActionKind.CustomerPayment, CustomerName = "منى" };
        var p = await r.Svc.ProposeAsync("منى دفعت");
        Assert.NotNull(p.InvoiceId);
        Assert.Equal(300m, p.OutstandingAmount);
        Assert.Equal(300m, p.Amount);
    }

    [Fact]
    public async Task Execute_Payment_RecordsOnTheChosenInvoice()
    {
        var r = await BuildAsync();
        var id = Guid.NewGuid();
        await r.Svc.ExecuteAsync(new ExecuteActionRequestDto { Kind = AssistantActionKind.CustomerPayment, InvoiceId = id, Amount = 120, Method = 2 });
        Assert.Equal(id, r.Sales.Paid!.Value.Id);
        Assert.Equal(120m, r.Sales.Paid.Value.Dto.Amount);
        Assert.Equal(PaymentTerm.Card, r.Sales.Paid.Value.Dto.Method);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ExecuteAsync(new ExecuteActionRequestDto { Kind = AssistantActionKind.CustomerPayment, InvoiceId = id, Amount = 0 }));
    }

    [Fact]
    public async Task MenuPrice_ProposesThenChangesOnlyAfterExecute()
    {
        var r = await BuildAsync();
        r.Parser.Intent = new ActionIntent { Kind = AssistantActionKind.MenuPrice, ItemName = "قهوة", NewPrice = 50 };
        var p = await r.Svc.ProposeAsync("غيّر سعر القهوة 50");
        Assert.Equal(40m, p.OldPrice);
        Assert.Equal(40m, (await r.Ctx.Items.FirstAsync()).MenuPrice);
        var res = await r.Svc.ExecuteAsync(new ExecuteActionRequestDto { Kind = AssistantActionKind.MenuPrice, ItemId = p.ItemId, NewPrice = 50 });
        Assert.Contains("50", res.Message);
        Assert.Equal(50m, (await r.Ctx.Items.FirstAsync()).MenuPrice);
    }

    [Fact]
    public async Task NotAnAction_ReturnsTheQuestionAndNothingElse()
    {
        var r = await BuildAsync();
        r.Parser.Intent = new ActionIntent { Kind = AssistantActionKind.None, Question = "ده سؤال مش إجراء." };
        var p = await r.Svc.ProposeAsync("هل أنا رابح؟");
        Assert.Equal(AssistantActionKind.None, p.Kind);
        Assert.Contains("سؤال", p.Summary);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ExecuteAsync(new ExecuteActionRequestDto { Kind = AssistantActionKind.None }));
    }

    [Fact]
    public void ModelAnswer_IsReadSafely_NullsAndUnknownKindsBecomeNone()
    {
        var read = ClaudeActionParser.Read(JsonNode.Parse("""{"kind":"sales_invoice","customer_name":"منى","payment_term":"cash","lines":[{"description":"قهوة","quantity":2,"unit_price":null}]}""")!.AsObject());
        Assert.Equal(AssistantActionKind.SalesInvoice, read.Kind);
        Assert.Null(read.Lines[0].UnitPrice);
        Assert.Equal(AssistantActionKind.None, ClaudeActionParser.Read(JsonNode.Parse("""{"kind":"delete_everything"}""")!.AsObject()).Kind);
    }
}
