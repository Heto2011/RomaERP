using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class DepreciationService : IDepreciationService
{
    private readonly IApplicationDbContext _context;

    public DepreciationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DepreciationRunDto>> GetAllAsync(CancellationToken ct = default)
    {
        var runs = await _context.DepreciationRuns
            .AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.FixedAsset)
            .OrderByDescending(r => r.RunDate)
            .ToListAsync(ct);

        return runs.Select(Map).ToList();
    }

    public async Task<DepreciationRunDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _context.DepreciationRuns
            .AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.FixedAsset)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(DepreciationRun), id);

        return Map(run);
    }

    public async Task<DepreciationRunDto> CreateAndCalculateAsync(CreateDepreciationRunDto dto, CancellationToken ct = default)
    {
        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);

        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن إنشاء قيد إهلاك لفترة محاسبية مقفلة.");

        var assets = await _context.FixedAssets
            .Where(a => !a.IsDeleted && a.Status == FixedAssetStatus.Active)
            .ToListAsync(ct);

        var run = new DepreciationRun
        {
            FiscalPeriodId = dto.FiscalPeriodId,
            RunDate = dto.RunDate,
            Description = dto.Description,
            Status = DepreciationRunStatus.Draft
        };

        foreach (var asset in assets)
        {
            var remainingDepreciable = asset.DepreciableBase - asset.AccumulatedDepreciation;
            if (remainingDepreciable <= 0)
                continue;

            var periodAmount = asset.DepreciationMethod == DepreciationMethod.StraightLine
                ? asset.DepreciableBase / asset.UsefulLifeYears / 12m
                : asset.BookValue * (asset.DecliningBalanceRate ?? 0) / 100m / 12m;

            periodAmount = Math.Min(periodAmount, remainingDepreciable);
            if (periodAmount <= 0)
                continue;

            run.Lines.Add(new DepreciationRunLine
            {
                FixedAssetId = asset.Id,
                Amount = Math.Round(periodAmount, 2)
            });
        }

        if (run.Lines.Count == 0)
            throw new ValidationAppException("لا يوجد أصول مستحق عليها إهلاك حاليًا.");

        _context.DepreciationRuns.Add(run);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(run.Id, ct);
    }

    public async Task<DepreciationRunDto> PostAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _context.DepreciationRuns
            .Include(r => r.Lines).ThenInclude(l => l.FixedAsset)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(DepreciationRun), id);

        if (run.Status != DepreciationRunStatus.Draft)
            throw new ValidationAppException("لا يمكن ترحيل قيد إهلاك إلا في حالة المسودة.");

        var depreciationExpenseAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Code == AccountingConstants.DepreciationExpenseAccountCode && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب مصروف الإهلاك ({AccountingConstants.DepreciationExpenseAccountCode}) غير موجود في دليل الحسابات.");

        var amountsByAccumulatedDepreciationAccount = new Dictionary<Guid, decimal>();
        decimal totalAmount = 0;

        foreach (var line in run.Lines)
        {
            totalAmount += line.Amount;
            var accountId = line.FixedAsset!.AccumulatedDepreciationAccountId;
            amountsByAccumulatedDepreciationAccount[accountId] = amountsByAccumulatedDepreciationAccount.GetValueOrDefault(accountId) + line.Amount;
        }

        var lines = new List<JournalEntryLine>
        {
            new()
            {
                LineNumber = 1,
                AccountId = depreciationExpenseAccount.Id,
                Debit = totalAmount,
                Credit = 0,
                Description = $"مصروف إهلاك - {run.RunDate:yyyy-MM}"
            }
        };

        var lineNumber = 2;
        foreach (var (accountId, amount) in amountsByAccumulatedDepreciationAccount)
        {
            lines.Add(new JournalEntryLine
            {
                LineNumber = lineNumber++,
                AccountId = accountId,
                Debit = 0,
                Credit = amount,
                Description = $"مجمع إهلاك - {run.RunDate:yyyy-MM}"
            });
        }

        var entryNumber = $"JV-{(await _context.JournalEntries.CountAsync(ct) + 1):D6}";
        var journalEntry = new JournalEntry
        {
            EntryNumber = entryNumber,
            EntryDate = run.RunDate,
            FiscalPeriodId = run.FiscalPeriodId,
            Description = $"قيد إهلاك أصول ثابتة - {run.RunDate:yyyy-MM}",
            Reference = AccountingConstants.DepreciationRunReference,
            Status = JournalEntryStatus.Posted,
            Lines = lines
        };

        _context.JournalEntries.Add(journalEntry);
        run.JournalEntryId = journalEntry.Id;
        run.Status = DepreciationRunStatus.Posted;

        foreach (var line in run.Lines)
            line.FixedAsset!.AccumulatedDepreciation += line.Amount;

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static DepreciationRunDto Map(DepreciationRun r) => new()
    {
        Id = r.Id,
        FiscalPeriodId = r.FiscalPeriodId,
        RunDate = r.RunDate,
        Status = r.Status,
        Description = r.Description,
        JournalEntryId = r.JournalEntryId,
        Lines = r.Lines.Select(l => new DepreciationRunLineDto
        {
            FixedAssetId = l.FixedAssetId,
            AssetCode = l.FixedAsset?.Code ?? string.Empty,
            AssetName = l.FixedAsset?.NameAr ?? string.Empty,
            Amount = l.Amount
        }).ToList()
    };
}
