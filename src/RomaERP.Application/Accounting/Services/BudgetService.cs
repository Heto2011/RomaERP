using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class BudgetService : IBudgetService
{
    private readonly IApplicationDbContext _context;

    public BudgetService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BudgetLineDto>> GetBudgetLinesAsync(Guid fiscalYearId, CancellationToken ct = default)
    {
        var periodIds = await _context.FiscalPeriods
            .AsNoTracking()
            .Where(p => p.FiscalYearId == fiscalYearId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        return await _context.Budgets
            .AsNoTracking()
            .Include(b => b.Account)
            .Include(b => b.FiscalPeriod)
            .Where(b => periodIds.Contains(b.FiscalPeriodId))
            .OrderBy(b => b.Account!.Code)
            .ThenBy(b => b.FiscalPeriod!.StartDate)
            .Select(b => new BudgetLineDto
            {
                Id = b.Id,
                AccountId = b.AccountId,
                AccountCode = b.Account!.Code,
                AccountName = b.Account.NameAr,
                FiscalPeriodId = b.FiscalPeriodId,
                FiscalPeriodName = b.FiscalPeriod!.Name,
                Amount = b.Amount
            })
            .ToListAsync(ct);
    }

    public async Task<BudgetLineDto> SetBudgetLineAsync(SetBudgetLineDto dto, CancellationToken ct = default)
    {
        if (dto.Amount < 0)
            throw new ValidationAppException("قيمة الموازنة لا يمكن أن تكون سالبة.");

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == dto.AccountId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Account), dto.AccountId);
        if (account.AccountType != AccountType.Revenue && account.AccountType != AccountType.Expense)
            throw new ValidationAppException("الموازنة التقديرية بتتحدد بس على حسابات الإيرادات والمصروفات.");
        if (account.IsControlAccount)
            throw new ValidationAppException($"لا يمكن وضع موازنة على حساب إجمالي ({account.Code}).");

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);

        var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.AccountId == dto.AccountId && b.FiscalPeriodId == dto.FiscalPeriodId, ct);
        if (budget is not null)
        {
            budget.Amount = dto.Amount;
        }
        else
        {
            budget = new Budget { AccountId = dto.AccountId, FiscalPeriodId = dto.FiscalPeriodId, Amount = dto.Amount };
            _context.Budgets.Add(budget);
        }

        await _context.SaveChangesAsync(ct);

        return new BudgetLineDto
        {
            Id = budget.Id,
            AccountId = account.Id,
            AccountCode = account.Code,
            AccountName = account.NameAr,
            FiscalPeriodId = period.Id,
            FiscalPeriodName = period.Name,
            Amount = budget.Amount
        };
    }

    public async Task<BudgetVsActualReportDto> GetBudgetVsActualAsync(Guid fiscalYearId, CancellationToken ct = default)
    {
        var fiscalYear = await _context.FiscalYears
            .AsNoTracking()
            .Include(y => y.Periods)
            .FirstOrDefaultAsync(y => y.Id == fiscalYearId, ct)
            ?? throw new NotFoundException(nameof(FiscalYear), fiscalYearId);

        var periodIds = fiscalYear.Periods.Select(p => p.Id).ToList();

        var budgets = await _context.Budgets
            .AsNoTracking()
            .Where(b => periodIds.Contains(b.FiscalPeriodId))
            .ToListAsync(ct);

        var actualLines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate >= fiscalYear.StartDate
                        && l.JournalEntry.EntryDate <= fiscalYear.EndDate
                        && (l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
            .ToListAsync(ct);

        var accountIds = budgets.Select(b => b.AccountId)
            .Union(actualLines.Select(l => l.AccountId))
            .Distinct()
            .ToList();

        var accountsById = (await _context.Accounts
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToListAsync(ct))
            .ToDictionary(a => a.Id);

        var revenueLines = new List<BudgetVsActualLineDto>();
        var expenseLines = new List<BudgetVsActualLineDto>();

        foreach (var accountId in accountIds)
        {
            if (!accountsById.TryGetValue(accountId, out var account)) continue;

            var budgetedAmount = budgets.Where(b => b.AccountId == accountId).Sum(b => b.Amount);
            var accountActualLines = actualLines.Where(l => l.AccountId == accountId).ToList();
            var debit = accountActualLines.Sum(l => l.Debit);
            var credit = accountActualLines.Sum(l => l.Credit);
            var actualAmount = account.AccountType == AccountType.Revenue ? credit - debit : debit - credit;

            if (budgetedAmount == 0 && actualAmount == 0) continue;

            var line = new BudgetVsActualLineDto
            {
                AccountId = account.Id,
                AccountCode = account.Code,
                AccountName = account.NameAr,
                AccountType = account.AccountType,
                BudgetedAmount = budgetedAmount,
                ActualAmount = actualAmount,
                VarianceAmount = actualAmount - budgetedAmount,
                VariancePercent = budgetedAmount != 0 ? Math.Round((actualAmount - budgetedAmount) / Math.Abs(budgetedAmount) * 100, 2) : null
            };

            (account.AccountType == AccountType.Revenue ? revenueLines : expenseLines).Add(line);
        }

        revenueLines = revenueLines.OrderBy(l => l.AccountCode).ToList();
        expenseLines = expenseLines.OrderBy(l => l.AccountCode).ToList();

        var totalBudgetedRevenue = revenueLines.Sum(l => l.BudgetedAmount);
        var totalActualRevenue = revenueLines.Sum(l => l.ActualAmount);
        var totalBudgetedExpense = expenseLines.Sum(l => l.BudgetedAmount);
        var totalActualExpense = expenseLines.Sum(l => l.ActualAmount);

        return new BudgetVsActualReportDto
        {
            FiscalYearId = fiscalYear.Id,
            FiscalYearName = fiscalYear.Name,
            RevenueLines = revenueLines,
            ExpenseLines = expenseLines,
            TotalBudgetedRevenue = totalBudgetedRevenue,
            TotalActualRevenue = totalActualRevenue,
            TotalBudgetedExpense = totalBudgetedExpense,
            TotalActualExpense = totalActualExpense,
            BudgetedNetIncome = totalBudgetedRevenue - totalBudgetedExpense,
            ActualNetIncome = totalActualRevenue - totalActualExpense
        };
    }
}
