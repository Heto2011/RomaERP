using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

/// <summary>Moves what an old account holds to a new one with a single posted adjustment entry, so a company that changed its chart can
/// empty the old accounts without touching the original entries (they stay as they were). Nothing is posted until the preview is approved.
/// Only balance-sheet accounts of the same type are moved: moving income or expense between periods is an accountant's call.</summary>
public class AccountBalanceTransferService : IAccountBalanceTransferService
{
    public const string Reference = "ACCOUNT-TRANSFER";
    private readonly IApplicationDbContext _context;

    public AccountBalanceTransferService(IApplicationDbContext context) => _context = context;

    public async Task<BalanceTransferPreviewDto> PreviewAsync(BalanceTransferRequestDto request, CancellationToken ct = default)
    {
        var (from, to, blocked) = await LoadAsync(request, ct);
        var preview = new BalanceTransferPreviewDto
        {
            FromCode = from?.Code ?? string.Empty, FromName = from?.NameAr ?? string.Empty,
            ToCode = to?.Code ?? string.Empty, ToName = to?.NameAr ?? string.Empty,
            Blocked = blocked,
        };
        if (blocked is not null || from is null || to is null) return preview;

        var balance = await BalanceAsync(from.Id, request.Date, ct);
        preview.Balance = balance;
        preview.Amount = Math.Abs(balance);
        if (balance == 0) { preview.Blocked = "رصيد الحساب القديم صفر، مفيش حاجة تتنقل."; return preview; }

        var debitIsNew = balance > 0;      // a debit balance moves as a debit in the new account and a credit in the old one
        preview.DebitAccount = debitIsNew ? $"{to.Code} - {to.NameAr}" : $"{from.Code} - {from.NameAr}";
        preview.CreditAccount = debitIsNew ? $"{from.Code} - {from.NameAr}" : $"{to.Code} - {to.NameAr}";
        preview.Description = $"نقل رصيد الحساب {from.Code} ({from.NameAr}) إلى {to.Code} ({to.NameAr}) بعد تغيير شجرة الحسابات";
        preview.CanApply = true;
        return preview;
    }

    public async Task<BalanceTransferResultDto> ApplyAsync(BalanceTransferRequestDto request, CancellationToken ct = default)
    {
        var preview = await PreviewAsync(request, ct);
        if (!preview.CanApply) throw new ValidationAppException(preview.Blocked ?? "مينفعش ينفّذ النقل.");

        var date = request.Date.Date;
        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && !p.IsClosed, ct)
            ?? throw new ValidationAppException("مفيش فترة محاسبية مفتوحة تغطي التاريخ ده.");

        var debitId = preview.Balance > 0 ? request.ToAccountId : request.FromAccountId;
        var creditId = preview.Balance > 0 ? request.FromAccountId : request.ToAccountId;
        var entry = await SimpleJournalEntryFactory.CreatePostedAsync(_context, date, period.Id, preview.Description, debitId, creditId, preview.Amount, Reference, ct);
        await _context.SaveChangesAsync(ct);
        return new BalanceTransferResultDto { EntryNumber = entry.EntryNumber, Amount = preview.Amount };
    }

    private async Task<(Account? From, Account? To, string? Blocked)> LoadAsync(BalanceTransferRequestDto request, CancellationToken ct)
    {
        if (request.FromAccountId == request.ToAccountId) return (null, null, "اختار حسابين مختلفين.");
        var from = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.FromAccountId, ct);
        var to = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.ToAccountId, ct);
        if (from is null || to is null) return (from, to, "اختار الحساب القديم والحساب الجديد.");
        if (from.AccountType != to.AccountType) return (from, to, "الحسابين لازم يبقوا من نفس النوع (أصول مع أصول، خصوم مع خصوم...).");
        if (from.AccountType is AccountType.Revenue or AccountType.Expense)
            return (from, to, "نقل أرصدة الإيرادات والمصروفات بيتعمل بقيد تسوية من المحاسب، لأنه بيأثر على أرباح الفترة.");
        if (await _context.Accounts.AnyAsync(a => a.ParentAccountId == to.Id, ct))
            return (from, to, "الحساب الجديد حساب رئيسي (له فروع)، اختار حساب فرعي تسجَّل عليه حركات.");
        return (from, to, null);
    }

    private async Task<decimal> BalanceAsync(Guid accountId, DateTime date, CancellationToken ct)
    {
        var end = date.Date.AddDays(1);
        var lines = await _context.JournalEntryLines.AsNoTracking()
            .Where(l => l.AccountId == accountId && l.JournalEntry!.EntryDate < end
                        && (l.JournalEntry.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Reversed))
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync(ct);
        return lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit);
    }
}
