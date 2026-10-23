using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.Services;

public class RecurringExpenseDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public int DayOfMonth { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime? LastPreparedMonth { get; set; }
}

/// <summary>Turns an expense that was just captured into a fixed monthly item.</summary>
public class MarkRecurringDto
{
    /// <summary>True: the amount is the same every month and is pre-filled. False: it is typed in each month.</summary>
    public bool AmountIsFixed { get; set; } = true;
    public int? DayOfMonth { get; set; }
}

public interface IRecurringExpenseService
{
    Task<List<RecurringExpenseDto>> GetAllAsync(CancellationToken ct = default);
    Task<RecurringExpenseDto> SaveAsync(Guid? id, RecurringExpenseDto dto, CancellationToken ct = default);
    Task<RecurringExpenseDto> MarkFromCaptureAsync(Guid captureId, MarkRecurringDto dto, string userId, CancellationToken ct = default);
    Task<RecurringExpenseDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Prepares this month's expense for every active item that has come due and has not been prepared yet.
    /// They land in the approvals list; nothing is posted. Returns how many were prepared.</summary>
    Task<int> PrepareDueAsync(DateTime today, string userId, CancellationToken ct = default);
}

public class RecurringExpenseService : IRecurringExpenseService
{
    private readonly IApplicationDbContext _context;

    public RecurringExpenseService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RecurringExpenseDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _context.RecurringExpenses.AsNoTracking().Include(r => r.Account)
            .OrderBy(r => r.DayOfMonth).ThenBy(r => r.Description).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<RecurringExpenseDto> SaveAsync(Guid? id, RecurringExpenseDto dto, CancellationToken ct = default)
    {
        var description = TextLimit.Check(dto.Description, 200, "اسم البند") ?? throw new ValidationAppException("اكتب اسم البند (مثلًا: إيجار المحل).");
        if (dto.DayOfMonth is < 1 or > 28) throw new ValidationAppException("يوم الشهر لازم يكون من 1 لـ 28.");
        if (dto.Amount is <= 0) throw new ValidationAppException("المبلغ لازم يكون أكبر من صفر.");
        if (dto.PaymentMethod != PaymentMethod.Cash && dto.PaymentMethod != PaymentMethod.Card)
            throw new ValidationAppException("اختار طريقة الدفع: نقدًا أو شبكة.");
        var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == dto.AccountId && !a.IsDeleted, ct)
            ?? throw new ValidationAppException("اختار حساب المصروف.");
        if (account.AccountType != AccountType.Expense)
            throw new ValidationAppException("الحساب لازم يكون حساب مصروفات.");

        RecurringExpense item;
        if (id is { } existing)
            item = await _context.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == existing, ct) ?? throw new NotFoundException(nameof(RecurringExpense), existing);
        else
        {
            item = new RecurringExpense();
            _context.RecurringExpenses.Add(item);
        }
        item.Description = description;
        item.AccountId = dto.AccountId;
        item.Amount = dto.Amount;
        item.Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "EGP" : dto.Currency.Trim();
        item.PaymentMethod = dto.PaymentMethod;
        item.DayOfMonth = dto.DayOfMonth;
        item.IsActive = dto.IsActive;
        await _context.SaveChangesAsync(ct);
        return await GetOneAsync(item.Id, ct);
    }

    public async Task<RecurringExpenseDto> MarkFromCaptureAsync(Guid captureId, MarkRecurringDto dto, string userId, CancellationToken ct = default)
    {
        var capture = await _context.ExpenseCaptures.AsNoTracking().FirstOrDefaultAsync(c => c.Id == captureId, ct)
            ?? throw new NotFoundException(nameof(ExpenseCapture), captureId);
        if (capture.SuggestedAccountId is null || string.IsNullOrWhiteSpace(capture.Description))
            throw new ValidationAppException("المصروف ده ناقصه الحساب أو الوصف، كمّله الأول وبعدين علّمه كبند ثابت.");
        if (capture.PaymentMethod != PaymentMethod.Cash && capture.PaymentMethod != PaymentMethod.Card)
            throw new ValidationAppException("حدد الأول إن المصروف نقدًا أو شبكة.");
        if (dto.AmountIsFixed && capture.Amount is not > 0)
            throw new ValidationAppException("المصروف ده مفيهوش مبلغ — اختار إن المبلغ بيتغير كل شهر.");
        var day = Math.Clamp(dto.DayOfMonth ?? capture.EntryDate.Day, 1, 28);

        // Marking the same expense twice must not create two items.
        var already = await _context.RecurringExpenses.AnyAsync(r => r.IsActive && r.AccountId == capture.SuggestedAccountId
            && r.Description == capture.Description, ct);
        if (already) throw new ValidationAppException("البند ده متعلّم كبند ثابت قبل كده.");

        var item = new RecurringExpense
        {
            Description = capture.Description!.Trim(),
            AccountId = capture.SuggestedAccountId.Value,
            Amount = dto.AmountIsFixed ? capture.Amount : null,
            Currency = capture.Currency,
            PaymentMethod = capture.PaymentMethod,
            DayOfMonth = day,
            IsActive = true,
            // This month's expense already exists (the one being marked), so the first automatic one is next month's.
            LastPreparedMonth = new DateTime(capture.EntryDate.Year, capture.EntryDate.Month, 1)
        };
        _context.RecurringExpenses.Add(item);
        await _context.SaveChangesAsync(ct);
        return await GetOneAsync(item.Id, ct);
    }

    public async Task<RecurringExpenseDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var item = await _context.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(RecurringExpense), id);
        item.IsActive = active;
        await _context.SaveChangesAsync(ct);
        return await GetOneAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _context.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(RecurringExpense), id);
        item.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> PrepareDueAsync(DateTime today, string userId, CancellationToken ct = default)
    {
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var due = await _context.RecurringExpenses
            .Where(r => r.IsActive && r.DayOfMonth <= today.Day && (r.LastPreparedMonth == null || r.LastPreparedMonth < monthStart))
            .ToListAsync(ct);
        foreach (var item in due)
        {
            var dueDate = new DateTime(today.Year, today.Month, item.DayOfMonth);
            _context.ExpenseCaptures.Add(new ExpenseCapture
            {
                RawText = $"بند ثابت: {item.Description}",
                Amount = item.Amount,
                Currency = item.Currency,
                Description = item.Description,
                EntryDate = dueDate,
                SuggestedAccountId = item.AccountId,
                FundingSource = FundingSource.CompanyAccount,
                PaymentMethod = item.PaymentMethod,
                // Cash goes straight to the approver; a card payment waits to be matched with the bank statement like any other.
                Status = item.PaymentMethod == PaymentMethod.Card ? ExpenseCaptureStatus.AwaitingReconciliation : ExpenseCaptureStatus.PendingApproval,
                SubmittedByUserId = userId
            });
            item.LastPreparedMonth = monthStart;
        }
        if (due.Count > 0) await _context.SaveChangesAsync(ct);
        return due.Count;
    }

    private async Task<RecurringExpenseDto> GetOneAsync(Guid id, CancellationToken ct)
        => Map(await _context.RecurringExpenses.AsNoTracking().Include(r => r.Account).FirstAsync(r => r.Id == id, ct));

    private static RecurringExpenseDto Map(RecurringExpense r) => new()
    {
        Id = r.Id, Description = r.Description, AccountId = r.AccountId, AccountCode = r.Account?.Code, AccountName = r.Account?.NameAr,
        Amount = r.Amount, Currency = r.Currency, PaymentMethod = r.PaymentMethod, DayOfMonth = r.DayOfMonth, IsActive = r.IsActive,
        LastPreparedMonth = r.LastPreparedMonth
    };
}
