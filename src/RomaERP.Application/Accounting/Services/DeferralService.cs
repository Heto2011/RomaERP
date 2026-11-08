using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class DeferralService : IDeferralService
{
    private readonly IApplicationDbContext _context;

    public DeferralService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeferralDto>> GetAllAsync(CancellationToken ct = default)
        => (await _context.DeferralSchedules.AsNoTracking().Include(d => d.TargetAccount)
                .OrderBy(d => d.Status).ThenByDescending(d => d.StartMonth).ToListAsync(ct))
            .Select(Map).ToList();

    public async Task<DeferralDto> CreateAsync(CreateDeferralDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Description)) throw new ValidationAppException("اكتب وصف للمبلغ (مثلاً: إيجار سنة).");
        if (dto.Description.Trim().Length > 200) throw new ValidationAppException("الوصف طويل (أقصى حد 200 حرف).");
        if (dto.TotalAmount <= 0) throw new ValidationAppException("المبلغ لازم يكون أكبر من صفر.");
        if (dto.Months is < 2 or > 120) throw new ValidationAppException("عدد الشهور لازم يكون من 2 لـ120.");

        var target = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == dto.TargetAccountId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Account), dto.TargetAccountId);
        var settlement = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == dto.SettlementAccountId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Account), dto.SettlementAccountId);

        var prepaid = dto.Kind == DeferralKind.PrepaidExpense;
        if (prepaid && target.AccountType != AccountType.Expense)
            throw new ValidationAppException("حساب المصروف لازم يكون من نوع مصروفات.");
        if (!prepaid && target.AccountType != AccountType.Revenue)
            throw new ValidationAppException("حساب الإيراد لازم يكون من نوع إيرادات.");
        if (settlement.AccountType is not (AccountType.Asset or AccountType.Liability))
            throw new ValidationAppException("حساب الدفع/التحصيل لازم يكون صندوق أو بنك أو عملاء أو موردين.");

        var deferralAccountCode = prepaid ? AccountingConstants.PrepaidExpensesAccountCode : AccountingConstants.UnearnedRevenueAccountCode;
        var deferralAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == deferralAccountCode && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب {(prepaid ? "المصروفات المقدمة" : "الإيرادات المقدمة")} ({deferralAccountCode}) غير موجود في دليل الحسابات.");

        var entryDate = dto.EntryDate == default ? DateTime.UtcNow.Date : dto.EntryDate.Date;
        var period = await FindOpenPeriodAsync(entryDate, ct)
            ?? throw new ValidationAppException("مفيش فترة مالية مفتوحة لتاريخ القيد ده.");

        var description = dto.Description.Trim();
        var total = Math.Round(dto.TotalAmount, 2);
        // Prepaid expense: money goes out (credit the cash side), asset goes up. Deferred revenue: money comes in, liability goes up.
        var debit = prepaid ? deferralAccount : settlement;
        var credit = prepaid ? settlement : deferralAccount;
        var entry = await SimpleJournalEntryFactory.CreatePostedAsync(_context, entryDate, period.Id,
            (prepaid ? "مصروف مقدم - " : "إيراد مقدم - ") + description, debit.Id, credit.Id, total, AccountingConstants.DeferralReference, ct);

        var schedule = new DeferralSchedule
        {
            Kind = dto.Kind,
            Description = description,
            TotalAmount = total,
            StartMonth = new DateTime(dto.StartMonth.Year, dto.StartMonth.Month, 1),
            Months = dto.Months,
            TargetAccountId = target.Id,
            SettlementAccountId = settlement.Id,
            SourceJournalEntryId = entry.Id,
            Status = DeferralStatus.Active
        };
        _context.DeferralSchedules.Add(schedule);
        await _context.SaveChangesAsync(ct);

        schedule.TargetAccount = target;
        return Map(schedule);
    }

    public async Task<DeferralRunResultDto> RunAsync(DateTime? upToMonth = null, CancellationToken ct = default)
    {
        var limit = upToMonth ?? DateTime.UtcNow;
        var limitMonth = new DateTime(limit.Year, limit.Month, 1);
        var today = DateTime.UtcNow.Date;
        var result = new DeferralRunResultDto();

        var schedules = await _context.DeferralSchedules.Include(d => d.TargetAccount)
            .Where(d => d.Status == DeferralStatus.Active).ToListAsync(ct);

        foreach (var schedule in schedules)
        {
            var deferralCode = schedule.Kind == DeferralKind.PrepaidExpense ? AccountingConstants.PrepaidExpensesAccountCode : AccountingConstants.UnearnedRevenueAccountCode;
            var deferralAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == deferralCode && !a.IsDeleted, ct);
            if (deferralAccount is null)
            {
                result.Skipped.Add($"{schedule.Description}: حساب {deferralCode} غير موجود في دليل الحسابات.");
                continue;
            }

            while (schedule.RecognizedMonths < schedule.Months)
            {
                var month = schedule.StartMonth.AddMonths(schedule.RecognizedMonths);
                if (month > limitMonth) break;

                var monthEnd = month.AddMonths(1).AddDays(-1);
                var entryDate = monthEnd < today ? monthEnd : today;
                var period = await FindOpenPeriodAsync(entryDate, ct);
                if (period is null)
                {
                    result.Skipped.Add($"{schedule.Description}: مفيش فترة مالية مفتوحة لشهر {month:yyyy-MM}.");
                    break;
                }

                var isLast = schedule.RecognizedMonths == schedule.Months - 1;
                var amount = isLast ? schedule.TotalAmount - schedule.RecognizedAmount : Math.Round(schedule.TotalAmount / schedule.Months, 2);
                if (amount > 0)
                {
                    var prepaid = schedule.Kind == DeferralKind.PrepaidExpense;
                    var text = $"{(prepaid ? "تحميل مصروف" : "تحقيق إيراد")} {month:yyyy-MM} — {schedule.Description}";
                    await SimpleJournalEntryFactory.CreatePostedAsync(_context, entryDate, period.Id, text,
                        prepaid ? schedule.TargetAccountId : deferralAccount.Id,
                        prepaid ? deferralAccount.Id : schedule.TargetAccountId,
                        amount, AccountingConstants.DeferralReference, ct);
                    result.EntriesPosted++;
                    result.AmountReleased += amount;
                    schedule.RecognizedAmount += amount;
                }
                schedule.RecognizedMonths++;
                // Save as we go so the next entry number is computed from a current count.
                await _context.SaveChangesAsync(ct);
            }

            if (schedule.RecognizedMonths >= schedule.Months)
                schedule.Status = DeferralStatus.Completed;
        }

        await _context.SaveChangesAsync(ct);
        return result;
    }

    public async Task<DeferralDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var schedule = await _context.DeferralSchedules.Include(d => d.TargetAccount).FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException(nameof(DeferralSchedule), id);
        if (schedule.Status != DeferralStatus.Active)
            throw new ValidationAppException("الجدول ده مش شغال أصلاً.");
        schedule.Status = DeferralStatus.Cancelled;
        await _context.SaveChangesAsync(ct);
        return Map(schedule);
    }

    private Task<FiscalPeriod?> FindOpenPeriodAsync(DateTime date, CancellationToken ct)
        => _context.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && !p.IsClosed, ct);

    private static DeferralDto Map(DeferralSchedule d) => new()
    {
        Id = d.Id,
        Kind = d.Kind,
        Description = d.Description,
        TotalAmount = d.TotalAmount,
        StartMonth = d.StartMonth,
        Months = d.Months,
        RecognizedMonths = d.RecognizedMonths,
        RecognizedAmount = d.RecognizedAmount,
        Remaining = d.TotalAmount - d.RecognizedAmount,
        MonthlyAmount = Math.Round(d.TotalAmount / d.Months, 2),
        TargetAccountName = d.TargetAccount?.NameAr ?? string.Empty,
        Status = d.Status
    };
}
