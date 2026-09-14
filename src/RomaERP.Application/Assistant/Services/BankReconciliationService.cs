using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.Services;

/// <summary>
/// Imports a bank statement (CSV: Date,Description,Amount — Amount positive for money leaving the
/// account) and matches its lines against card expenses captured through the AI assistant that are
/// still waiting on reconciliation. A match moves the capture to PendingApproval — it still needs
/// Admin sign-off (ExpenseAssistantService.ApproveAsync) before the journal entry is posted.
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
            throw new ValidationAppException("لم يتم العثور على أي حركات في الملف المرفوع. تأكد إن فيه عمود تاريخ وعمود مبلغ (أو مدين/دائن) — بالعربي أو الإنجليزي.");

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

    /// <summary>Sign convention here: positive = money leaving the account (matches an outgoing card
    /// expense) — when the file gives separate Debit/Credit columns instead of one signed Amount, that
    /// resolves to Debit - Credit (the opposite of BankFeedReconciliationService's convention, since that
    /// one reconciles deposits/withdrawals against the GL rather than matching card-expense captures).</summary>
    private static async Task<List<BankStatementLine>> ParseCsvAsync(Stream csvStream, CancellationToken ct)
    {
        var parsed = await BankStatementCsvParser.ParseAsync(csvStream, ct);

        return parsed.Select(p => new BankStatementLine
        {
            TransactionDate = p.Date,
            Description = p.Description,
            Amount = p.Amount ?? ((p.Debit ?? 0) - (p.Credit ?? 0))
        }).ToList();
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

            MarkMatched(capture, candidates[0]);
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

        MarkMatched(capture, line);
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

    /// <summary>
    /// Marks the bank line and capture as matched but does NOT post a journal entry — matched card
    /// expenses still need Admin approval (see ExpenseAssistantService.ApproveAsync) before they hit the GL.
    /// </summary>
    private static void MarkMatched(ExpenseCapture capture, BankStatementLine line)
    {
        line.IsMatched = true;
        capture.MatchedBankStatementLineId = line.Id;
        capture.Status = ExpenseCaptureStatus.PendingApproval;
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
