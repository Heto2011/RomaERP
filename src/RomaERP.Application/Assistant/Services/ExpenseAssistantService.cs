using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.Services;

public class ExpenseAssistantService : IExpenseAssistantService
{
    private static readonly string[] CashKeywords = { "كاش", "نقد", "نقدي", "cash" };
    private static readonly string[] CardKeywords = { "شبكة", "شبكه", "فيزا", "فيزه", "بطاقة", "بطاقه", "كارت", "card", "فوري", "انستاباي" };

    private readonly IApplicationDbContext _context;
    private readonly IClaudeExpenseParser _parser;

    public ExpenseAssistantService(IApplicationDbContext context, IClaudeExpenseParser parser)
    {
        _context = context;
        _parser = parser;
    }

    public async Task<ChatTurnResponseDto> SendMessageAsync(ChatTurnRequestDto request, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ValidationAppException("الرسالة لا يمكن أن تكون فارغة.");

        ExpenseCapture capture;
        if (request.CaptureId is { } id)
        {
            capture = await _context.ExpenseCaptures
                .Include(c => c.SuggestedAccount)
                .FirstOrDefaultAsync(c => c.Id == id, ct)
                ?? throw new NotFoundException(nameof(ExpenseCapture), id);
        }
        else
        {
            capture = new ExpenseCapture
            {
                RawText = request.Message,
                EntryDate = DateTime.UtcNow.Date,
                SubmittedByUserId = userId,
                Status = ExpenseCaptureStatus.AwaitingDetails
            };
            _context.ExpenseCaptures.Add(capture);
        }

        _context.ExpenseCaptureMessages.Add(new ExpenseCaptureMessage { ExpenseCaptureId = capture.Id, Role = ChatRole.User, Content = request.Message });

        var assistantReply = capture.Status switch
        {
            ExpenseCaptureStatus.AwaitingDetails => await HandleDetailsAsync(capture, request.Message, ct),
            ExpenseCaptureStatus.AwaitingPaymentMethod => HandlePaymentMethod(capture, request.Message),
            _ => "المصروف ده اتسجل خلاصه، مفيش حاجة تانية مطلوبة منك دلوقتي في نفس المحادثة."
        };

        _context.ExpenseCaptureMessages.Add(new ExpenseCaptureMessage { ExpenseCaptureId = capture.Id, Role = ChatRole.Assistant, Content = assistantReply });

        await _context.SaveChangesAsync(ct);

        var history = await _context.ExpenseCaptureMessages
            .AsNoTracking()
            .Where(m => m.ExpenseCaptureId == capture.Id)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        return new ChatTurnResponseDto
        {
            CaptureId = capture.Id,
            Status = capture.Status,
            AssistantReply = assistantReply,
            History = history.Select(m => new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Content,
                CreatedAtUtc = m.CreatedAtUtc
            }).ToList(),
            Capture = Map(capture)
        };
    }

    private async Task<string> HandleDetailsAsync(ExpenseCapture capture, string message, CancellationToken ct)
    {
        var candidates = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.AccountType == AccountType.Expense && !a.IsControlAccount && a.IsActive && !a.IsDeleted)
            .Select(a => new ExpenseAccountCandidate(a.Code, a.NameAr))
            .ToListAsync(ct);

        var priorContext = capture.Amount is null && capture.RawText != message ? capture.RawText : null;
        var result = await _parser.ExtractAsync(message, priorContext, candidates, ct);

        if (result.NeedsClarification || result.Amount is null)
        {
            capture.RawText = string.IsNullOrWhiteSpace(priorContext) ? message : $"{priorContext} | {message}";
            return result.ClarifyingQuestion ?? "ممكن تقولي المبلغ بالظبط؟";
        }

        capture.Amount = result.Amount;
        capture.Currency = string.IsNullOrWhiteSpace(result.Currency) ? "EGP" : result.Currency!;
        capture.Description = string.IsNullOrWhiteSpace(result.Description) ? message : result.Description;

        var account = await ResolveAccountAsync(result.SuggestedAccountCode, ct);
        capture.SuggestedAccountId = account.Id;
        capture.SuggestedAccount = account;
        capture.Status = ExpenseCaptureStatus.AwaitingPaymentMethod;

        return $"تمام، هسجل \"{capture.Description}\" بمبلغ {capture.Amount} {capture.Currency} تحت بند ({account.Code} - {account.NameAr}). الدفع كان كاش ولا شبكة؟";
    }

    private static string HandlePaymentMethod(ExpenseCapture capture, string message)
    {
        var normalized = message.Trim();
        var isCash = CashKeywords.Any(k => normalized.Contains(k, StringComparison.OrdinalIgnoreCase));
        var isCard = CardKeywords.Any(k => normalized.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (!isCash && !isCard)
            return "من فضلك اكتب \"كاش\" أو \"شبكة\" عشان أقدر أسجل المصروف صح.";

        if (isCash)
        {
            capture.PaymentMethod = PaymentMethod.Cash;
            capture.Status = ExpenseCaptureStatus.PendingApproval;

            return "تمام، المصروف اتسجل وهيتراجع من المدير للاعتماد قبل ما يترحّل نهائيًا في الحسابات.";
        }

        capture.PaymentMethod = PaymentMethod.Card;
        capture.Status = ExpenseCaptureStatus.AwaitingReconciliation;

        return "تمام، المصروف ده هيفضل معلّق لحد ما نستورد كشف حساب البنك ونطابقه تلقائيًا. لو معاك صورة الإيصال ارفعها من شاشة المصروفات كإثبات.";
    }

    private async Task<Account> ResolveAccountAsync(string? suggestedCode, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(suggestedCode))
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Code == suggestedCode && a.AccountType == AccountType.Expense && !a.IsControlAccount && !a.IsDeleted, ct);
            if (account is not null)
                return account;
        }

        return await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.GeneralAdminExpenseAccountCode && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب المصروفات الإدارية ({AccountingConstants.GeneralAdminExpenseAccountCode}) غير موجود في دليل الحسابات.");
    }

    private async Task<FiscalPeriod> FindOpenPeriodAsync(DateTime date, CancellationToken ct)
    {
        return await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && !p.IsClosed, ct)
            ?? throw new ValidationAppException("لا توجد فترة محاسبية مفتوحة تغطي تاريخ هذا المصروف.");
    }

    public async Task<List<ExpenseCaptureDto>> GetPendingReconciliationAsync(CancellationToken ct = default)
    {
        var captures = await _context.ExpenseCaptures
            .AsNoTracking()
            .Include(c => c.SuggestedAccount)
            .Where(c => c.Status == ExpenseCaptureStatus.AwaitingReconciliation && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        return captures.Select(Map).ToList();
    }

    public async Task<List<ExpenseCaptureDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var captures = await _context.ExpenseCaptures
            .AsNoTracking()
            .Include(c => c.SuggestedAccount)
            .Where(c => c.Status == ExpenseCaptureStatus.PendingApproval && !c.IsDeleted)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        return captures.Select(Map).ToList();
    }

    public async Task<ExpenseCaptureDto> ApproveAsync(Guid captureId, CancellationToken ct = default)
    {
        var capture = await _context.ExpenseCaptures
            .Include(c => c.SuggestedAccount)
            .Include(c => c.MatchedBankStatementLine).ThenInclude(l => l!.BankStatementImport)
            .FirstOrDefaultAsync(c => c.Id == captureId, ct)
            ?? throw new NotFoundException(nameof(ExpenseCapture), captureId);

        if (capture.Status != ExpenseCaptureStatus.PendingApproval)
            throw new ValidationAppException("هذا المصروف ليس في انتظار الاعتماد.");

        Guid creditAccountId;
        DateTime entryDate;

        if (capture.PaymentMethod == PaymentMethod.Cash)
        {
            var cashAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.CashOnHandAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب الصندوق ({AccountingConstants.CashOnHandAccountCode}) غير موجود في دليل الحسابات.");
            creditAccountId = cashAccount.Id;
            entryDate = capture.EntryDate;
        }
        else
        {
            if (capture.MatchedBankStatementLine?.BankStatementImport is null)
                throw new ValidationAppException("لا يوجد تطابق بنكي مؤكد لهذا المصروف بعد.");

            creditAccountId = capture.MatchedBankStatementLine.BankStatementImport.BankAccountId;
            entryDate = capture.MatchedBankStatementLine.TransactionDate;
        }

        var period = await FindOpenPeriodAsync(entryDate, ct);

        var entry = await SimpleJournalEntryFactory.CreatePostedAsync(
            _context, entryDate, period.Id,
            $"مصروف عبر المساعد الذكي (معتمد): {capture.Description}",
            debitAccountId: capture.SuggestedAccountId!.Value,
            creditAccountId: creditAccountId,
            amount: capture.Amount!.Value,
            reference: "AI-ASSISTANT",
            ct: ct);

        capture.JournalEntry = entry;
        capture.Status = ExpenseCaptureStatus.Posted;

        await _context.SaveChangesAsync(ct);
        return Map(capture);
    }

    public async Task<ExpenseCaptureDto> RejectAsync(Guid captureId, CancellationToken ct = default)
    {
        var capture = await _context.ExpenseCaptures
            .Include(c => c.SuggestedAccount)
            .Include(c => c.MatchedBankStatementLine)
            .FirstOrDefaultAsync(c => c.Id == captureId, ct)
            ?? throw new NotFoundException(nameof(ExpenseCapture), captureId);

        if (capture.Status != ExpenseCaptureStatus.PendingApproval)
            throw new ValidationAppException("هذا المصروف ليس في انتظار الاعتماد.");

        if (capture.MatchedBankStatementLine is not null)
            capture.MatchedBankStatementLine.IsMatched = false;

        capture.MatchedBankStatementLineId = null;
        capture.Status = ExpenseCaptureStatus.Rejected;

        await _context.SaveChangesAsync(ct);
        return Map(capture);
    }

    public async Task<ExpenseCaptureDto> AttachProofAsync(Guid captureId, string fileName, string storagePath, CancellationToken ct = default)
    {
        var capture = await _context.ExpenseCaptures.FirstOrDefaultAsync(c => c.Id == captureId, ct)
            ?? throw new NotFoundException(nameof(ExpenseCapture), captureId);

        capture.ProofFileName = fileName;
        capture.ProofStoragePath = storagePath;
        await _context.SaveChangesAsync(ct);

        return Map(capture);
    }

    private static ExpenseCaptureDto Map(ExpenseCapture c) => new()
    {
        Id = c.Id,
        RawText = c.RawText,
        Amount = c.Amount,
        Currency = c.Currency,
        Description = c.Description,
        EntryDate = c.EntryDate,
        SuggestedAccountId = c.SuggestedAccountId,
        SuggestedAccountCode = c.SuggestedAccount?.Code,
        SuggestedAccountName = c.SuggestedAccount?.NameAr,
        PaymentMethod = c.PaymentMethod,
        Status = c.Status,
        ProofFileName = c.ProofFileName,
        JournalEntryId = c.JournalEntryId,
        SubmittedByUserId = c.SubmittedByUserId
    };
}
