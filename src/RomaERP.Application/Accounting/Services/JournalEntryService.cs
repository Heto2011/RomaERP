using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class JournalEntryService : IJournalEntryService
{
    private readonly IApplicationDbContext _context;

    public JournalEntryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<JournalEntryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entries = await _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines).ThenInclude(l => l.Account)
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.EntryNumber)
            .ToListAsync(ct);

        return entries.Select(Map).ToList();
    }

    public async Task<JournalEntryDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(JournalEntry), id);

        return Map(entry);
    }

    public async Task<JournalEntryDto> CreateAsync(CreateJournalEntryDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count < 2)
            throw new ValidationAppException("القيد يجب أن يحتوي على سطرين على الأقل.");

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);

        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن إضافة قيود لفترة محاسبية مقفلة.");

        decimal totalDebit = 0, totalCredit = 0;
        var lines = new List<JournalEntryLine>();
        var lineNumber = 1;

        foreach (var lineDto in dto.Lines)
        {
            if (lineDto.Debit < 0 || lineDto.Credit < 0)
                throw new ValidationAppException("قيمة المدين/الدائن لا يمكن أن تكون سالبة.");

            if (lineDto.Debit > 0 && lineDto.Credit > 0)
                throw new ValidationAppException("لا يمكن أن يحتوي السطر على مدين ودائن معًا.");

            if (lineDto.Debit == 0 && lineDto.Credit == 0)
                throw new ValidationAppException("يجب إدخال قيمة مدين أو دائن لكل سطر.");

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == lineDto.AccountId, ct)
                ?? throw new NotFoundException(nameof(Account), lineDto.AccountId);

            if (account.IsControlAccount)
                throw new ValidationAppException($"لا يمكن الترحيل على حساب إجمالي ({account.Code}).");

            if (!account.IsActive)
                throw new ValidationAppException($"الحساب ({account.Code}) غير نشط.");

            totalDebit += lineDto.Debit;
            totalCredit += lineDto.Credit;

            lines.Add(new JournalEntryLine
            {
                LineNumber = lineNumber++,
                AccountId = lineDto.AccountId,
                CostCenterId = lineDto.CostCenterId,
                Debit = lineDto.Debit,
                Credit = lineDto.Credit,
                Description = lineDto.Description
            });
        }

        if (totalDebit != totalCredit)
            throw new ValidationAppException($"القيد غير متوازن: إجمالي المدين {totalDebit} لا يساوي إجمالي الدائن {totalCredit}.");

        var entryNumber = await GenerateEntryNumberAsync(ct);

        var entry = new JournalEntry
        {
            EntryNumber = entryNumber,
            EntryDate = dto.EntryDate,
            FiscalPeriodId = dto.FiscalPeriodId,
            Description = dto.Description,
            Reference = dto.Reference,
            Status = JournalEntryStatus.Draft,
            Lines = lines
        };

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(entry.Id, ct);
    }

    public async Task<JournalEntryDto> PostAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.JournalEntries
            .Include(e => e.Lines)
            .Include(e => e.FiscalPeriod)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(JournalEntry), id);

        if (entry.Status != JournalEntryStatus.Draft)
            throw new ValidationAppException("لا يمكن ترحيل قيد إلا إذا كان في حالة مسودة.");

        if (entry.FiscalPeriod is { IsClosed: true })
            throw new ValidationAppException("لا يمكن الترحيل على فترة محاسبية مقفلة.");

        if (entry.TotalDebit != entry.TotalCredit || entry.Lines.Count < 2)
            throw new ValidationAppException("القيد غير متوازن، لا يمكن ترحيله.");

        entry.Status = JournalEntryStatus.Posted;
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<JournalEntryDto> ReverseAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(nameof(JournalEntry), id);

        if (entry.Status != JournalEntryStatus.Posted)
            throw new ValidationAppException("لا يمكن عكس إلا القيود المرحلة.");

        var reversalNumber = await GenerateEntryNumberAsync(ct);
        var reversal = new JournalEntry
        {
            EntryNumber = reversalNumber,
            EntryDate = DateTime.UtcNow.Date,
            FiscalPeriodId = entry.FiscalPeriodId,
            Description = $"عكس قيد رقم {entry.EntryNumber}",
            Reference = entry.EntryNumber,
            Status = JournalEntryStatus.Posted,
            Lines = entry.Lines.Select((l, idx) => new JournalEntryLine
            {
                LineNumber = idx + 1,
                AccountId = l.AccountId,
                CostCenterId = l.CostCenterId,
                Debit = l.Credit,
                Credit = l.Debit,
                Description = l.Description
            }).ToList()
        };

        entry.Status = JournalEntryStatus.Reversed;
        _context.JournalEntries.Add(reversal);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(reversal.Id, ct);
    }

    public async Task<List<TrialBalanceLineDto>> GetTrialBalanceAsync(DateTime? asOfDate, CancellationToken ct = default)
    {
        var query = _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted && !l.JournalEntry.IsDeleted);

        if (asOfDate is { } date)
            query = query.Where(l => l.JournalEntry!.EntryDate <= date);

        var lines = await query.ToListAsync(ct);

        var result = lines
            .GroupBy(l => l.Account!)
            .Select(g =>
            {
                var totalDebit = g.Sum(l => l.Debit);
                var totalCredit = g.Sum(l => l.Credit);
                var balance = g.Key.Nature == AccountNature.Debit
                    ? totalDebit - totalCredit
                    : totalCredit - totalDebit;

                return new TrialBalanceLineDto
                {
                    AccountCode = g.Key.Code,
                    AccountName = g.Key.NameAr,
                    AccountType = g.Key.AccountType,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    Balance = balance
                };
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        return result;
    }

    private async Task<string> GenerateEntryNumberAsync(CancellationToken ct)
    {
        var count = await _context.JournalEntries.CountAsync(ct);
        return $"JV-{(count + 1):D6}";
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
