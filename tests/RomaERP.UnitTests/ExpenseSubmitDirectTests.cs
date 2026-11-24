using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;
using RomaERP.Domain.HR;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ExpenseSubmitDirectTests
{
    private static async Task<(ExpenseAssistantService Svc, ApplicationDbContext Ctx, Account Expense, Employee Emp)> BuildAsync()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var expense = new Account { Code = "5300", NameAr = "كهرباء", NameEn = "Electricity", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "مبيعات", NameEn = "Sales", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        ctx.Accounts.AddRange(expense, revenue);
        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = "Y", StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "P", PeriodNumber = 1, StartDate = today.AddDays(-10), EndDate = today.AddDays(10) });
        var dept = new Department { Code = "D", NameAr = "إدارة", NameEn = "Admin" };
        var pos = new Position { Code = "P", TitleAr = "موظف", TitleEn = "Staff", DepartmentId = dept.Id };
        var emp = new Employee { EmployeeCode = "E1", FullNameAr = "سارة", FullNameEn = "Sara", DepartmentId = dept.Id, PositionId = pos.Id, EmploymentStatus = EmploymentStatus.Active };
        ctx.Departments.Add(dept); ctx.Positions.Add(pos); ctx.Employees.Add(emp);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "ش", CompanyNameEn = "Co", Country = Country.Egypt, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();
        return (new ExpenseAssistantService(ctx, null!), ctx, expense, emp);
    }

    [Fact]
    public async Task CashExpense_WaitsForApproval_AndPostsNothing()
    {
        var r = await BuildAsync();
        var dto = await r.Svc.SubmitDirectAsync(new SubmitExpenseDto { Description = "فاتورة كهرباء", Amount = 850, AccountId = r.Expense.Id, FundingSource = 1, PaymentMethod = 1 }, "u1");
        Assert.Equal(ExpenseCaptureStatus.PendingApproval, dto.Status);
        var saved = await r.Ctx.ExpenseCaptures.SingleAsync();
        Assert.Null(saved.JournalEntryId);
        Assert.Equal(850m, saved.Amount);
        Assert.Equal("EGP", saved.Currency);
        Assert.Empty(await r.Ctx.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task CardExpense_WaitsForTheBankStatement_AndCustodyNeedsARealEmployee()
    {
        var r = await BuildAsync();
        var card = await r.Svc.SubmitDirectAsync(new SubmitExpenseDto { Description = "x", Amount = 10, AccountId = r.Expense.Id, FundingSource = 1, PaymentMethod = 2 }, "u1");
        Assert.Equal(ExpenseCaptureStatus.AwaitingReconciliation, card.Status);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitDirectAsync(new SubmitExpenseDto { Description = "x", Amount = 10, AccountId = r.Expense.Id, FundingSource = 2 }, "u1"));
        var custody = await r.Svc.SubmitDirectAsync(new SubmitExpenseDto { Description = "x", Amount = 10, AccountId = r.Expense.Id, FundingSource = 2, CustodyEmployeeId = r.Emp.Id }, "u1");
        Assert.Equal(ExpenseCaptureStatus.PendingApproval, custody.Status);
    }

    [Fact]
    public async Task BadInput_IsRefused()
    {
        var r = await BuildAsync();
        var revenue = await r.Ctx.Accounts.FirstAsync(a => a.Code == "4100");
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitDirectAsync(new SubmitExpenseDto { Description = "x", Amount = 10, AccountId = revenue.Id }, "u1"));   // not an expense account
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitDirectAsync(new SubmitExpenseDto { Description = "x", Amount = 0, AccountId = r.Expense.Id }, "u1"));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitDirectAsync(new SubmitExpenseDto { Description = " ", Amount = 5, AccountId = r.Expense.Id }, "u1"));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitDirectAsync(new SubmitExpenseDto { Description = "x", Amount = 5, AccountId = r.Expense.Id, EntryDate = DateTime.UtcNow.AddDays(3) }, "u1"));
        Assert.Empty(await r.Ctx.ExpenseCaptures.ToListAsync());
    }
}
