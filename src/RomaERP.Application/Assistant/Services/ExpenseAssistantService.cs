using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;
using RomaERP.Domain.HR;

namespace RomaERP.Application.Assistant.Services;

public class ExpenseAssistantService : IExpenseAssistantService
{
    private static readonly string[] CashKeywords = { "كاش", "نقد", "نقدي", "cash" };
    private static readonly string[] CardKeywords = { "شبكة", "شبكه", "فيزا", "فيزه", "بطاقة", "بطاقه", "كارت", "card", "فوري", "انستاباي" };
    private static readonly string[] CustodyKeywords = { "عهدة", "عهده", "عهدتي" };
    private static readonly string[] CompanyAccountKeywords = { "جاري", "الشركة", "شركة", "company" };

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
            ExpenseCaptureStatus.AwaitingFundingSource => HandleFundingSource(capture, request.Message),
            ExpenseCaptureStatus.AwaitingCustodyEmployee => await HandleCustodyEmployeeAsync(capture, request.Message, ct),
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
        capture.Status = ExpenseCaptureStatus.AwaitingFundingSource;

        return $"تمام، هسجل \"{capture.Description}\" بمبلغ {capture.Amount} {capture.Currency} تحت بند ({account.Code} - {account.NameAr}). الصرف ده من عهدتك ولا من جاري الشركة؟";
    }

    private static string HandleFundingSource(ExpenseCapture capture, string message)
    {
        var normalized = message.Trim();
        var isCustody = CustodyKeywords.Any(k => normalized.Contains(k, StringComparison.OrdinalIgnoreCase));
        var isCompanyAccount = CompanyAccountKeywords.Any(k => normalized.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (!isCustody && !isCompanyAccount)
            return "من فضلك اكتب \"عهدة\" لو الصرف من عهدتك، أو \"جاري\" لو من حساب الشركة مباشرة.";

        if (isCustody)
        {
            capture.FundingSource = FundingSource.EmployeeCustody;
            capture.Status = ExpenseCaptureStatus.AwaitingCustodyEmployee;
            return "تمام، عهدة مين؟ اكتب اسم الموظف أو كوده.";
        }

        capture.FundingSource = FundingSource.CompanyAccount;
        capture.Status = ExpenseCaptureStatus.AwaitingPaymentMethod;
        return "تمام، الدفع كان كاش ولا شبكة؟";
    }

    private async Task<string> HandleCustodyEmployeeAsync(ExpenseCapture capture, string message, CancellationToken ct)
    {
        var query = message.Trim();

        var matches = await _context.Employees
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.EmploymentStatus == EmploymentStatus.Active
                        && (e.EmployeeCode == query || e.FullNameAr.Contains(query) || e.FullNameEn.Contains(query)))
            .ToListAsync(ct);

        if (matches.Count == 0)
            return $"مش لاقي موظف بالاسم أو الكود \"{query}\"، جرب تاني.";

        if (matches.Count > 1)
        {
            var options = string.Join("، ", matches.Select(e => $"{e.EmployeeCode} - {e.FullNameAr}"));
            return $"في أكتر من موظف بنفس الاسم، اكتب الكود بالظبط: {options}";
        }

        var employee = matches[0];
        capture.CustodyEmployeeId = employee.Id;
        capture.Status = ExpenseCaptureStatus.PendingApproval;

        return $"تمام، هيتسجل على عهدة {employee.FullNameAr} وهيتراجع من المدير للاعتماد قبل ما يترحّل نهائيًا.";
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
            .Include(c => c.CustodyEmployee)
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
            .Include(c => c.CustodyEmployee)
            .FirstOrDefaultAsync(c => c.Id == captureId, ct)
            ?? throw new NotFoundException(nameof(ExpenseCapture), captureId);

        if (capture.Status != ExpenseCaptureStatus.PendingApproval)
            throw new ValidationAppException("هذا المصروف ليس في انتظار الاعتماد.");

        Guid creditAccountId;
        DateTime entryDate;
        string descriptionSuffix;

        if (capture.FundingSource == FundingSource.EmployeeCustody)
        {
            if (capture.CustodyEmployee is null)
                throw new ValidationAppException("لم يتم تحديد الموظف صاحب العهدة لهذا المصروف.");

            var custodyAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.EmployeeCustodyAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب عهد الموظفين ({AccountingConstants.EmployeeCustodyAccountCode}) غير موجود في دليل الحسابات.");

            creditAccountId = custodyAccount.Id;
            entryDate = capture.EntryDate;
            descriptionSuffix = $"من عهدة {capture.CustodyEmployee.FullNameAr}";

            capture.CustodyEmployee.CustodyBalance -= capture.Amount!.Value;
        }
        else if (capture.PaymentMethod == PaymentMethod.Cash)
        {
            var cashAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.CashOnHandAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب الصندوق ({AccountingConstants.CashOnHandAccountCode}) غير موجود في دليل الحسابات.");
            creditAccountId = cashAccount.Id;
            entryDate = capture.EntryDate;
            descriptionSuffix = "نقدًا";
        }
        else
        {
            if (capture.MatchedBankStatementLine?.BankStatementImport is null)
                throw new ValidationAppException("لا يوجد تطابق بنكي مؤكد لهذا المصروف بعد.");

            creditAccountId = capture.MatchedBankStatementLine.BankStatementImport.BankAccountId;
            entryDate = capture.MatchedBankStatementLine.TransactionDate;
            descriptionSuffix = "عبر الشبكة (مطابق بكشف الحساب)";
        }

        var period = await FindOpenPeriodAsync(entryDate, ct);

        var entry = await SimpleJournalEntryFactory.CreatePostedAsync(
            _context, entryDate, period.Id,
            $"مصروف عبر المساعد الذكي (معتمد) {descriptionSuffix}: {capture.Description}",
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
        FundingSource = c.FundingSource,
        CustodyEmployeeId = c.CustodyEmployeeId,
        CustodyEmployeeName = c.CustodyEmployee?.FullNameAr,
        PaymentMethod = c.PaymentMethod,
        Status = c.Status,
        ProofFileName = c.ProofFileName,
        JournalEntryId = c.JournalEntryId,
        SubmittedByUserId = c.SubmittedByUserId
    };
}
