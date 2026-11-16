using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class EmployeeDebtTests
{
    private record S(ApplicationDbContext Ctx, EmployeeDebtService Debts, PayrollService Payroll, Employee Emp, FiscalPeriod Period);

    private static async Task<S> CreateAsync()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var emp = new Employee { EmployeeCode = "E1", FullNameAr = "محمد", FullNameEn = "Mohamed", HireDate = new DateTime(2025, 1, 1), BasicSalary = 3000 };
        var period = new FiscalPeriod { Name = "Sep", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.AddRange(emp, period);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "ش", CompanyNameEn = "Co", PayrollDaysPerMonth = 30, Country = Country.UAE });
        ctx.Accounts.AddRange(
            new Account { Code = AccountingConstants.SalariesExpenseAccountCode, NameAr = "a", NameEn = "Salaries", AccountType = AccountType.Expense, Nature = AccountNature.Debit },
            new Account { Code = AccountingConstants.AccruedSalariesPayableAccountCode, NameAr = "b", NameEn = "Accrued", AccountType = AccountType.Liability, Nature = AccountNature.Credit },
            new Account { Code = AccountingConstants.EmployeeCustodyAccountCode, NameAr = "c", NameEn = "Custody", AccountType = AccountType.Asset, Nature = AccountNature.Debit },
            new Account { Code = AccountingConstants.CashOnHandAccountCode, NameAr = "d", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit });
        await ctx.SaveChangesAsync();
        return new S(ctx, new EmployeeDebtService(ctx), new PayrollService(ctx), emp, period);
    }

    private static CreateAdvanceDto Advance(S s, decimal amount, decimal? installment = null) => new() { EmployeeId = s.Emp.Id, Amount = amount, DebtDate = new DateTime(2026, 9, 5), MonthlyInstallment = installment };

    [Fact]
    public async Task Advance_PostsToEmployeeCustodyAgainstCash_AndOpensADebt()
    {
        var s = await CreateAsync();
        var d = await s.Debts.CreateAdvanceAsync(Advance(s, 1000), "owner");
        Assert.Equal(1000m, d.RemainingAmount);
        var lines = await s.Ctx.JournalEntryLines.Include(l => l.Account).ToListAsync();
        Assert.Contains(lines, l => l.Account!.Code == "1170" && l.Debit == 1000m);
        Assert.Contains(lines, l => l.Account!.Code == "1111" && l.Credit == 1000m);
    }

    [Fact]
    public async Task TheSystemOnlyProposes_NothingChangesUntilTheAccountantApproves()
    {
        var s = await CreateAsync();
        await s.Debts.CreateAdvanceAsync(Advance(s, 1000, 400), "owner");
        var run = await s.Payroll.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = s.Period.Id, RunDate = new DateTime(2026, 9, 30) });
        var net = run.Lines.Single().NetSalary;

        var proposals = await s.Debts.ProposeForRunAsync(run.Id);
        Assert.Equal(400m, proposals.Single().ProposedAmount);                  // the instalment
        Assert.Equal(net, (await s.Ctx.PayrollRunLines.SingleAsync()).NetSalary); // untouched
    }

    [Fact]
    public async Task ApprovingWithAnEditedAmount_ReducesNetPay_AndPostingRepaysTheDebtThroughTheJournal()
    {
        var s = await CreateAsync();
        var debt = await s.Debts.CreateAdvanceAsync(Advance(s, 1000, 400), "owner");
        var run = await s.Payroll.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = s.Period.Id, RunDate = new DateTime(2026, 9, 30) });
        var net = run.Lines.Single().NetSalary;

        await s.Debts.ApproveForRunAsync(run.Id, new ApproveDeductionsDto { Items = { new ApproveDeductionItemDto { DebtId = debt.Id, Amount = 300 } } }, "accountant");
        Assert.Equal(net - 300m, (await s.Ctx.PayrollRunLines.SingleAsync()).NetSalary);

        // approving again replaces the earlier approval instead of stacking
        await s.Debts.ApproveForRunAsync(run.Id, new ApproveDeductionsDto { Items = { new ApproveDeductionItemDto { DebtId = debt.Id, Amount = 500 } } }, "accountant");
        Assert.Equal(net - 500m, (await s.Ctx.PayrollRunLines.SingleAsync()).NetSalary);
        Assert.Equal(1000m, (await s.Ctx.EmployeeDebts.SingleAsync()).RemainingAmount);   // not reduced until the run posts

        await s.Payroll.ApproveAsync(run.Id);
        await s.Payroll.PostAsync(run.Id);
        Assert.Equal(500m, (await s.Ctx.EmployeeDebts.SingleAsync()).RemainingAmount);
        var posted = await s.Ctx.PayrollRuns.SingleAsync();
        var entry = await s.Ctx.JournalEntries.Include(e => e.Lines).ThenInclude(l => l.Account).SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.True(entry.IsBalanced);
        Assert.Contains(entry.Lines, l => l.Account!.Code == "1170" && l.Credit == 500m);
    }

    [Fact]
    public async Task ADebtIsSettledWhenFullyRepaid_AndAmountsAboveWhatIsOwedOrEarnedAreRefused()
    {
        var s = await CreateAsync();
        var debt = await s.Debts.CreateAdvanceAsync(Advance(s, 200), "owner");
        var run = await s.Payroll.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = s.Period.Id, RunDate = new DateTime(2026, 9, 30) });
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Debts.ApproveForRunAsync(run.Id, new ApproveDeductionsDto { Items = { new ApproveDeductionItemDto { DebtId = debt.Id, Amount = 250 } } }, "a"));
        await s.Debts.ApproveForRunAsync(run.Id, new ApproveDeductionsDto { Items = { new ApproveDeductionItemDto { DebtId = debt.Id, Amount = 200 } } }, "a");
        await s.Payroll.ApproveAsync(run.Id);
        await s.Payroll.PostAsync(run.Id);
        Assert.Equal(EmployeeDebtStatus.Settled, (await s.Ctx.EmployeeDebts.SingleAsync()).Status);
    }

    [Fact]
    public async Task AStockShortageChargedToAnEmployee_AppearsInTheirDebtLedger()
    {
        var s = await CreateAsync();
        var cat = new RomaERP.Domain.Inventory.ItemCategory { Code = "C", NameAr = "خامات", NameEn = "Raw" };
        var wh = new RomaERP.Domain.Inventory.Warehouse { Code = "CAI", NameAr = "القاهرة", NameEn = "Cairo" };
        var item = new RomaERP.Domain.Inventory.Item { Code = "TOM", NameAr = "طماطم", NameEn = "Tomato", UnitOfMeasure = "kg", ItemCategory = cat, QuantityOnHand = 20, AverageCost = 10m };
        s.Ctx.AddRange(cat, wh, item);
        s.Ctx.Accounts.AddRange(
            new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit },
            new Account { Code = "5500", NameAr = "تكلفة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit });
        s.Ctx.FiscalYears.Add(new FiscalYear { Name = "Y", StartDate = DateTime.UtcNow.Date.AddMonths(-6), EndDate = DateTime.UtcNow.Date.AddMonths(6) });
        await s.Ctx.SaveChangesAsync();
        var year = await s.Ctx.FiscalYears.SingleAsync();
        s.Ctx.FiscalPeriods.Add(new FiscalPeriod { FiscalYearId = year.Id, Name = "P", PeriodNumber = 1, StartDate = DateTime.UtcNow.Date.AddDays(-20), EndDate = DateTime.UtcNow.Date.AddDays(20) });
        s.Ctx.StockMovements.Add(new RomaERP.Domain.Inventory.StockMovement { MovementNumber = "SM-000001", MovementDate = DateTime.UtcNow, MovementType = RomaERP.Domain.Inventory.StockMovementType.Receipt, ItemId = item.Id, WarehouseId = wh.Id, Quantity = 20, UnitCost = 10m, TotalCost = 200m });
        await s.Ctx.SaveChangesAsync();

        var counts = new RomaERP.Application.Inventory.Services.PhysicalStockCountService(s.Ctx, new RomaERP.Application.Inventory.Services.BranchStockService(s.Ctx),
            new RomaERP.Application.Inventory.Services.StockAdjustmentService(s.Ctx, new RomaERP.Application.Inventory.Services.ItemLotService(s.Ctx)), s.Debts);
        var c = await counts.CreateAsync(new RomaERP.Application.Inventory.DTOs.CreatePhysicalStockCountDto { ItemId = item.Id, WarehouseId = wh.Id, CountDate = DateTime.UtcNow.Date, CountedQuantity = 17 });
        await counts.ApplyAsync(c.Id, new RomaERP.Application.Inventory.DTOs.ApproveCountDto { Responsibility = 3, EmployeeId = s.Emp.Id }, "owner");

        var debt = await s.Ctx.EmployeeDebts.SingleAsync();
        Assert.Equal(30m, debt.RemainingAmount);
        Assert.Equal(EmployeeDebtReason.StockShortage, debt.Reason);
        Assert.Equal(s.Emp.Id, debt.EmployeeId);
    }
}
