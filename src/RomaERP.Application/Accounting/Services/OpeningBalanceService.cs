using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

/// <summary>
/// Handles the one-time "opening balances" import used when onboarding a client that already
/// has an existing set of books: they bring a trial balance from their old system and enter it
/// here as the starting point, instead of starting from zero.
/// </summary>
public class OpeningBalanceService : IOpeningBalanceService
{
    private readonly IApplicationDbContext _context;

    public OpeningBalanceService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<JournalEntryDto?> GetForFiscalYearAsync(Guid fiscalYearId, CancellationToken ct = default)
    {
        var entry = await _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines).ThenInclude(l => l.Account)
            .Include(e => e.FiscalPeriod)
            .Where(e => e.Reference == AccountingConstants.OpeningBalanceReference
                        && e.FiscalPeriod!.FiscalYearId == fiscalYearId
                        && e.Status != JournalEntryStatus.Reversed
                        && !e.IsDeleted)
            .FirstOrDefaultAsync(ct);

        return entry is null ? null : Map(entry);
    }

    public async Task<JournalEntryDto> CreateAsync(CreateOpeningBalanceDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count < 1)
            throw new ValidationAppException("يجب إدخال رصيد افتتاحي لحساب واحد على الأقل.");

        var period = await _context.FiscalPeriods
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);

        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن إدخال أرصدة افتتاحية في فترة مقفلة.");

        var existing = await _context.JournalEntries
            .AnyAsync(e => e.Reference == AccountingConstants.OpeningBalanceReference
                           && e.FiscalPeriod!.FiscalYearId == period.FiscalYearId
                           && e.Status != JournalEntryStatus.Reversed
                           && !e.IsDeleted, ct);

        if (existing)
            throw new ValidationAppException("يوجد بالفعل قيد أرصدة افتتاحية لهذه السنة المالية. لتعديله، اعكس القيد الحالي أولاً من شاشة القيود اليومية ثم أدخل الأرصدة الصحيحة.");

        decimal totalDebit = 0, totalCredit = 0;
        var lines = new List<JournalEntryLine>();
        var lineNumber = 1;
        var seenAccountIds = new HashSet<Guid>();

        foreach (var lineDto in dto.Lines)
        {
            if (!seenAccountIds.Add(lineDto.AccountId))
                throw new ValidationAppException("لا يمكن تكرار نفس الحساب أكثر من مرة في الأرصدة الافتتاحية.");

            if (lineDto.Debit < 0 || lineDto.Credit < 0)
                throw new ValidationAppException("قيمة الرصيد لا يمكن أن تكون سالبة.");

            if (lineDto.Debit > 0 && lineDto.Credit > 0)
                throw new ValidationAppException("لا يمكن إدخال رصيد مدين ودائن لنفس الحساب معًا.");

            if (lineDto.Debit == 0 && lineDto.Credit == 0)
                continue;

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == lineDto.AccountId, ct)
                ?? throw new NotFoundException(nameof(Account), lineDto.AccountId);

            if (account.IsControlAccount)
                throw new ValidationAppException($"لا يمكن إدخال رصيد افتتاحي على حساب إجمالي ({account.Code}).");

            totalDebit += lineDto.Debit;
            totalCredit += lineDto.Credit;

            lines.Add(new JournalEntryLine
            {
                LineNumber = lineNumber++,
                AccountId = lineDto.AccountId,
                Debit = lineDto.Debit,
                Credit = lineDto.Credit,
                Description = "رصيد افتتاحي"
            });
        }

        if (lines.Count < 2)
            throw new ValidationAppException("يجب إدخال رصيد افتتاحي لحسابين على الأقل حتى يتوازن القيد.");

        if (totalDebit != totalCredit)
            throw new ValidationAppException($"الأرصدة الافتتاحية غير متوازنة: إجمالي المدين {totalDebit} لا يساوي إجمالي الدائن {totalCredit}. تأكد من مطابقة الميزان المرحّل من النظام القديم بالكامل.");

        var entryNumber = $"JV-{(await _context.JournalEntries.CountAsync(ct) + 1):D6}";
        var entry = new JournalEntry
        {
            EntryNumber = entryNumber,
            EntryDate = dto.EntryDate,
            FiscalPeriodId = dto.FiscalPeriodId,
            Description = "قيد الأرصدة الافتتاحية",
            Reference = AccountingConstants.OpeningBalanceReference,
            Status = JournalEntryStatus.Posted,
            Lines = lines
        };

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync(ct);

        var saved = await _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines).ThenInclude(l => l.Account)
            .FirstAsync(e => e.Id == entry.Id, ct);

        return Map(saved);
    }

    private static JournalEntryDto Map(JournalEntry e) => new()
    {
        Id = e.Id,
        EntryNumber = e.EntryNumber,
        EntryDate = e.EntryDate,
        FiscalPeriodId = e.FiscalPeriodId,
        Description = e.Description,
        Reference = e.Reference,
        Status = e.Status,
        TotalDebit = e.TotalDebit,
        TotalCredit = e.TotalCredit,
        Lines = e.Lines.OrderBy(l => l.LineNumber).Select(l => new JournalEntryLineDto
        {
            Id = l.Id,
            LineNumber = l.LineNumber,
            AccountId = l.AccountId,
            AccountCode = l.Account?.Code ?? string.Empty,
            AccountName = l.Account?.NameAr ?? string.Empty,
            CostCenterId = l.CostCenterId,
            Debit = l.Debit,
            Credit = l.Credit,
            Description = l.Description
        }).ToList()
    };
}
