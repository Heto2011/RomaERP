using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class FiscalPeriodService : IFiscalPeriodService
{
    private readonly IApplicationDbContext _context;

    public FiscalPeriodService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<FiscalYearDto>> GetAllYearsAsync(CancellationToken ct = default)
    {
        var years = await _context.FiscalYears
            .AsNoTracking()
            .Include(y => y.Periods)
            .OrderByDescending(y => y.StartDate)
            .ToListAsync(ct);

        return years.Select(Map).ToList();
    }

    public async Task<FiscalPeriodDto> ClosePeriodAsync(Guid periodId, CancellationToken ct = default)
    {
        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == periodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), periodId);

        if (period.IsClosed)
            throw new ValidationAppException("الفترة مقفلة بالفعل.");

        var draftCount = await _context.JournalEntries
            .CountAsync(e => e.FiscalPeriodId == periodId && e.Status == JournalEntryStatus.Draft, ct);

        if (draftCount > 0)
            throw new ValidationAppException($"لا يمكن إقفال الفترة، يوجد {draftCount} قيد بحالة مسودة يجب ترحيله أو حذفه أولاً.");

        period.IsClosed = true;
        await _context.SaveChangesAsync(ct);

        return MapPeriod(period);
    }

    public async Task<FiscalPeriodDto> ReopenPeriodAsync(Guid periodId, CancellationToken ct = default)
    {
        var period = await _context.FiscalPeriods
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.Id == periodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), periodId);

        if (period.FiscalYear is { IsClosed: true })
            throw new ValidationAppException("لا يمكن فتح فترة تابعة لسنة مالية مقفلة. افتح السنة المالية أولاً.");

        period.IsClosed = false;
        await _context.SaveChangesAsync(ct);

        return MapPeriod(period);
    }

    public async Task<FiscalYearDto> CloseFiscalYearAsync(Guid fiscalYearId, CancellationToken ct = default)
    {
        var fiscalYear = await _context.FiscalYears
            .Include(y => y.Periods)
            .FirstOrDefaultAsync(y => y.Id == fiscalYearId, ct)
            ?? throw new NotFoundException(nameof(FiscalYear), fiscalYearId);

        if (fiscalYear.IsClosed)
            throw new ValidationAppException("السنة المالية مقفلة بالفعل.");

        var openPeriods = fiscalYear.Periods.Where(p => !p.IsClosed).ToList();
        if (openPeriods.Count > 0)
            throw new ValidationAppException(
                $"لا يمكن إقفال السنة المالية، الفترات التالية لسه مفتوحة: {string.Join(", ", openPeriods.Select(p => p.Name))}.");

        var lines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate >= fiscalYear.StartDate
                        && l.JournalEntry.EntryDate <= fiscalYear.EndDate
                        && (l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
            .ToListAsync(ct);

        var closingLines = new List<JournalEntryLine>();
        var lineNumber = 1;
        decimal netIncome = 0;

        foreach (var group in lines.Where(l => l.Account!.AccountType == AccountType.Revenue).GroupBy(l => l.AccountId))
        {
            var balance = group.Sum(l => l.Credit) - group.Sum(l => l.Debit);
            if (balance == 0) continue;

            netIncome += balance;
            closingLines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = group.Key, Debit = balance, Credit = 0, Description = "قيد إقفال - تصفير حساب إيراد" });
        }

        foreach (var group in lines.Where(l => l.Account!.AccountType == AccountType.Expense).GroupBy(l => l.AccountId))
        {
            var balance = group.Sum(l => l.Debit) - group.Sum(l => l.Credit);
            if (balance == 0) continue;

            netIncome -= balance;
            closingLines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = group.Key, Debit = 0, Credit = balance, Description = "قيد إقفال - تصفير حساب مصروف" });
        }

        if (closingLines.Count > 0 && netIncome != 0)
        {
            var retainedEarnings = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Code == AccountingConstants.RetainedEarningsAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب الأرباح المرحلة ({AccountingConstants.RetainedEarningsAccountCode}) غير موجود في دليل الحسابات.");

            closingLines.Add(netIncome > 0
                ? new JournalEntryLine { LineNumber = lineNumber, AccountId = retainedEarnings.Id, Debit = 0, Credit = netIncome, Description = "ترحيل صافي الربح إلى الأرباح المرحلة" }
                : new JournalEntryLine { LineNumber = lineNumber, AccountId = retainedEarnings.Id, Debit = -netIncome, Credit = 0, Description = "ترحيل صافي الخسارة إلى الأرباح المرحلة" });
        }

        if (closingLines.Count >= 2)
        {
            var lastPeriod = fiscalYear.Periods.OrderByDescending(p => p.EndDate).First();
            var entryNumber = $"JV-{(await _context.JournalEntries.CountAsync(ct) + 1):D6}";

            _context.JournalEntries.Add(new JournalEntry
            {
                EntryNumber = entryNumber,
                EntryDate = fiscalYear.EndDate,
                FiscalPeriodId = lastPeriod.Id,
                Description = $"قيد إقفال السنة المالية {fiscalYear.Name}",
                Status = JournalEntryStatus.Posted,
                Lines = closingLines
            });
        }

        fiscalYear.IsClosed = true;
        await _context.SaveChangesAsync(ct);

        return Map(fiscalYear);
    }

    private static FiscalYearDto Map(FiscalYear y) => new()
    {
        Id = y.Id,
        Name = y.Name,
        StartDate = y.StartDate,
        EndDate = y.EndDate,
        IsClosed = y.IsClosed,
        Periods = y.Periods.OrderBy(p => p.PeriodNumber).Select(MapPeriod).ToList()
    };

    private static FiscalPeriodDto MapPeriod(FiscalPeriod p) => new()
    {
        Id = p.Id,
        FiscalYearId = p.FiscalYearId,
        Name = p.Name,
        PeriodNumber = p.PeriodNumber,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        IsClosed = p.IsClosed
    };
}
