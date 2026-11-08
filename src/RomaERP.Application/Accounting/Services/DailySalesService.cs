using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class DailySalesService : IDailySalesService
{
    private readonly IApplicationDbContext _context;

    public DailySalesService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DailySalesEntryDto> CreateAsync(CreateDailySalesDto dto, CancellationToken ct = default)
    {
        if (dto.CashAmount < 0 || dto.CardAmount < 0)
            throw new ValidationAppException("المبالغ لازم تكون صفر أو أكبر.");
        var gross = Math.Round(dto.CashAmount + dto.CardAmount, 2);
        if (gross <= 0)
            throw new ValidationAppException("اكتب إجمالي المبيعات (كاش أو فيزا).");
        if (dto.VatRatePercent < 0 || dto.VatRatePercent > 100)
            throw new ValidationAppException("نسبة الضريبة لازم تكون بين 0 و 100.");

        var date = dto.SalesDate.Date;
        var label = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label.Trim();

        var duplicate = await _context.JournalEntries.AnyAsync(e =>
            e.Reference == AccountingConstants.DailySalesReference && !e.IsDeleted
            && e.Status != JournalEntryStatus.Reversed
            && e.EntryDate == date && (e.Description == DescriptionPrefix(label) || e.Description!.StartsWith(DescriptionPrefix(label) + " - ")), ct);
        if (duplicate)
            throw new ValidationAppException("في إجمالي مبيعات مسجّل لنفس اليوم والبيان. لو غلطت، اعكس القيد من الشاشة وسجّله تاني، أو اكتب بيان مختلف (مثلاً اسم الفرع).");

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && !p.IsClosed, ct)
            ?? throw new ValidationAppException("مفيش فترة مالية مفتوحة لتاريخ اليوم ده.");

        var vat = dto.VatRatePercent > 0
            ? Math.Round(gross - gross / (1 + dto.VatRatePercent / 100m), 2)
            : 0m;
        var net = gross - vat;

        var cash = dto.CashAmount > 0 ? await GetAccountAsync(AccountingConstants.CashOnHandAccountCode, "الصندوق", ct) : null;
        var bank = dto.CardAmount > 0 ? await GetAccountAsync(AccountingConstants.BankAccountCode, "البنك", ct) : null;
        var revenue = await GetAccountAsync(AccountingConstants.SalesRevenueAccountCode, "إيرادات المبيعات", ct);
        var vatAccount = vat > 0 ? await GetAccountAsync(AccountingConstants.OutputVatAccountCode, "ضريبة القيمة المضافة (مخرجات)", ct) : null;

        var description = DescriptionPrefix(label) + (string.IsNullOrWhiteSpace(dto.Notes) ? "" : $" - {dto.Notes.Trim()}");
        var lines = new List<JournalEntryLine>();
        if (cash is not null)
            lines.Add(new JournalEntryLine { AccountId = cash.Id, Debit = Math.Round(dto.CashAmount, 2), Credit = 0, Description = "مبيعات نقدية" });
        if (bank is not null)
            lines.Add(new JournalEntryLine { AccountId = bank.Id, Debit = Math.Round(dto.CardAmount, 2), Credit = 0, Description = "مبيعات فيزا / بنك" });
        lines.Add(new JournalEntryLine { AccountId = revenue.Id, Debit = 0, Credit = net, Description = "إيرادات مبيعات" });
        if (vatAccount is not null)
            lines.Add(new JournalEntryLine { AccountId = vatAccount.Id, Debit = 0, Credit = vat, Description = "ضريبة مخرجات" });
        for (var i = 0; i < lines.Count; i++) lines[i].LineNumber = i + 1;

        var entry = new JournalEntry
        {
            EntryNumber = $"JV-{(await _context.JournalEntries.CountAsync(ct) + 1):D6}",
            EntryDate = date,
            FiscalPeriodId = period.Id,
            Description = description,
            Reference = AccountingConstants.DailySalesReference,
            Status = JournalEntryStatus.Posted,
            Lines = lines
        };
        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync(ct);

        return new DailySalesEntryDto
        {
            JournalEntryId = entry.Id,
            EntryNumber = entry.EntryNumber,
            SalesDate = date,
            Label = label,
            CashAmount = Math.Round(dto.CashAmount, 2),
            CardAmount = Math.Round(dto.CardAmount, 2),
            NetSales = net,
            VatAmount = vat,
            Total = gross,
            Status = entry.Status.ToString()
        };
    }

    public async Task<List<DailySalesEntryDto>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var entries = await _context.JournalEntries
            .AsNoTracking()
            .Where(e => e.Reference == AccountingConstants.DailySalesReference && !e.IsDeleted)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.EntryNumber)
            .Take(take)
            .Select(e => new
            {
                e.Id, e.EntryNumber, e.EntryDate, e.Description, e.Status,
                Lines = e.Lines.Select(l => new { l.Account!.Code, l.Debit, l.Credit }).ToList()
            })
            .ToListAsync(ct);

        return entries.Select(e =>
        {
            var cash = e.Lines.Where(l => l.Code == AccountingConstants.CashOnHandAccountCode).Sum(l => l.Debit);
            var card = e.Lines.Where(l => l.Code == AccountingConstants.BankAccountCode).Sum(l => l.Debit);
            var vat = e.Lines.Where(l => l.Code == AccountingConstants.OutputVatAccountCode).Sum(l => l.Credit);
            var net = e.Lines.Where(l => l.Code == AccountingConstants.SalesRevenueAccountCode).Sum(l => l.Credit);
            return new DailySalesEntryDto
            {
                JournalEntryId = e.Id,
                EntryNumber = e.EntryNumber,
                SalesDate = e.EntryDate,
                Label = LabelFrom(e.Description),
                CashAmount = cash,
                CardAmount = card,
                NetSales = net,
                VatAmount = vat,
                Total = cash + card,
                Status = e.Status.ToString()
            };
        }).ToList();
    }

    private static string DescriptionPrefix(string? label)
        => string.IsNullOrEmpty(label) ? "إجمالي مبيعات اليوم" : $"إجمالي مبيعات اليوم [{label}]";

    private static string? LabelFrom(string? description)
    {
        if (description is null) return null;
        var start = description.IndexOf('[');
        var end = description.IndexOf(']');
        return start >= 0 && end > start ? description[(start + 1)..end] : null;
    }

    private async Task<Account> GetAccountAsync(string code, string arabicLabel, CancellationToken ct)
        => await _context.Accounts.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب {arabicLabel} ({code}) غير موجود في دليل الحسابات.");
}
