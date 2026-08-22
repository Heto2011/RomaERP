using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.Services;

/// <summary>
/// Imports a bank statement (CSV: Date,Description,Amount — Amount positive for money leaving the
/// account) and matches its lines against card expenses captured through the AI assistant that are
/// still waiting on reconciliation, posting the matching journal entry once matched.
/// </summary>
public class BankReconciliationService : IBankReconciliationService
{
    private static readonly TimeSpan MatchWindow = TimeSpan.FromDays(5);

    private readonly IApplicationDbContext _context;

    public BankReconciliationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BankStatementImportDto> ImportAsync(Stream csvStream, string fileName, Guid bankAccountId, string userId, CancellationToken ct = default)
    {
        var bankAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == bankAccountId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Account), bankAccountId);

        var lines = await ParseCsvAsync(csvStream, ct);
        if (lines.Count == 0)
            throw new ValidationAppException("لم يتم العثور على أي حركات في الملف المرفوع. تأكد من صيغة الملف: Date,Description,Amount.");

        var import = new BankStatementImport
        {
            FileName = fileName,
            BankAccountId = bankAccountId,
            PeriodFrom = lines.Min(l => l.TransactionDate),
            PeriodTo = lines.Max(l => l.TransactionDate),
            ImportedByUserId = userId,
            Lines = lines
        };

        _context.BankStatementImports.Add(import);
        await _context.SaveChangesAsync(ct);

        await AutoMatchAsync(ct);

        return new BankStatementImportDto
        {
            Id = import.Id,
            FileName = import.FileName,
            BankAccountName = bankAccount.NameAr,
            LineCount = import.Lines.Count,
            MatchedCount = import.Lines.Count(l => l.IsMatched)
        };
    }

    private static async Task<List<BankStatementLine>> ParseCsvAsync(Stream csvStream, CancellationToken ct)
    {
        using var reader = new StreamReader(csvStream);
        var lines = new List<BankStatementLine>();
        var isFirstLine = true;

        while (await reader.ReadLineAsync(ct) is { } rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var fields = rawLine.Split(',').Select(f => f.Trim().Trim('"')).ToArray();

            if (isFirstLine)
            {
                isFirstLine = false;
                if (fields.Length > 0 && !decimal.TryParse(fields.Last(), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    continue; // header row
            }

            if (fields.Length < 3)
                continue;

            if (!DateTime.TryParse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;

            if (!decimal.TryParse(fields[^1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                continue;

            var description = string.Join(",", fields.Skip(1).Take(fields.Length - 2));

            lines.Add(new BankStatementLine
            {
                TransactionDate = date,
                Description = description,
                Amount = amount
            });
        }

        return lines;
    }

    public async Task<List<BankStatementLineDto>> GetUnmatchedLinesAsync(CancellationToken ct = default)
    {
        var lines = await _context.BankStatementLines
            .AsNoTracking()
            .Where(l => !l.IsMatched)
            .OrderByDescending(l => l.TransactionDate)
            .ToListAsync(ct);

        return lines.Select(Map).ToList();
    }

    public async Task<int> AutoMatchAsync(CancellationToken ct = default)
    {
        var pendingCaptures = await _context.ExpenseCaptures
            .Where(c => c.Status == ExpenseCaptureStatus.AwaitingReconciliation && !c.IsDeleted)
            .ToListAsync(ct);

        if (pendingCaptures.Count == 0)
            return 0;

        var unmatchedLines = await _context.BankStatementLines
            .Include(l => l.BankStatementImport)
            .Where(l => !l.IsMatched)
            .ToListAsync(ct);

        var matchedCount = 0;

        foreach (var capture in pendingCaptures)
        {
            var candidates = unmatchedLines
                .Where(l => !l.IsMatched
                            && l.Amount == capture.Amount
                            && (l.TransactionDate - capture.EntryDate).Duration() <= MatchWindow)
                .ToList();

            if (candidates.Count != 1)
                continue;

            await PostMatchAsync(capture, candidates[0], ct);
            matchedCount++;
        }

        if (matchedCount > 0)
            await _context.SaveChangesAsync(ct);

        return matchedCount;
    }

    public async Task<ExpenseCaptureDto> MatchManualAsync(ManualMatchDto dto, CancellationToken ct = default)
    {
        var capture = await _context.ExpenseCaptures
            .Include(c => c.SuggestedAccount)
            .FirstOrDefaultAsync(c => c.Id == dto.ExpenseCaptureId, ct)
            ?? throw new NotFoundException(nameof(ExpenseCapture), dto.ExpenseCaptureId);

        if (capture.Status != ExpenseCaptureStatus.AwaitingReconciliation)
            throw new ValidationAppException("هذا المصروف ليس في انتظار المطابقة البنكية.");

        var line = await _context.BankStatementLines.FirstOrDefaultAsync(l => l.Id == dto.BankStatementLineId, ct)
            ?? throw new NotFoundException(nameof(BankStatementLine), dto.BankStatementLineId);

        if (line.IsMatched)
            throw new ValidationAppException("حركة كشف الحساب هذه متطابقة بالفعل مع مصروف آخر.");

        await PostMatchAsync(capture, line, ct);
        await _context.SaveChangesAsync(ct);

        return new ExpenseCaptureDto
        {
            Id = capture.Id,
            RawText = capture.RawText,
            Amount = capture.Amount,
            Currency = capture.Currency,
            Description = capture.Description,
            EntryDate = capture.EntryDate,
            SuggestedAccountId = capture.SuggestedAccountId,
            SuggestedAccountCode = capture.SuggestedAccount?.Code,
            SuggestedAccountName = capture.SuggestedAccount?.NameAr,
            PaymentMethod = capture.PaymentMethod,
            Status = capture.Status,
            ProofFileName = capture.ProofFileName,
            JournalEntryId = capture.JournalEntryId,
            SubmittedByUserId = capture.SubmittedByUserId
        };
    }

    private async Task PostMatchAsync(ExpenseCapture capture, BankStatementLine line, CancellationToken ct)
    {
        var period = await _context.FiscalPeriods
            .FirstOrDefaultAsync(p => p.StartDate <= line.TransactionDate && p.EndDate >= line.TransactionDate && !p.IsClosed, ct)
            ?? throw new ValidationAppException("لا توجد فترة محاسبية مفتوحة تغطي تاريخ حركة كشف الحساب.");

        var import = line.BankStatementImport ?? await _context.BankStatementImports.FirstAsync(i => i.Id == line.BankStatementImportId, ct);

        var entry = await SimpleJournalEntryFactory.CreatePostedAsync(
            _context, line.TransactionDate, period.Id,
            $"مصروف عبر المساعد الذكي (مطابق بكشف الحساب): {capture.Description}",
            debitAccountId: capture.SuggestedAccountId!.Value,
            creditAccountId: import.BankAccountId,
            amount: capture.Amount!.Value,
            reference: "AI-ASSISTANT",
            ct: ct);

        line.IsMatched = true;
        capture.MatchedBankStatementLineId = line.Id;
        capture.JournalEntry = entry;
        capture.Status = ExpenseCaptureStatus.Posted;
    }

    private static BankStatementLineDto Map(BankStatementLine l) => new()
    {
        Id = l.Id,
        TransactionDate = l.TransactionDate,
        Description = l.Description,
        Amount = l.Amount,
        IsMatched = l.IsMatched
    };
}
