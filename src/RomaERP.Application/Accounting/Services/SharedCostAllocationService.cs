using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public interface ISharedCostAllocationService
{
    Task<CostAllocationPreviewDto> PreviewAsync(int year, int month, CancellationToken ct = default);
    Task<Guid> ApplyAsync(ApplyCostAllocationDto dto, string? userName, CancellationToken ct = default);
}

/// <summary>The month-end allocation of costs that belong to no branch (head-office rent, office payroll, supplier expenses) to the branches, on
/// their share of the month's sales unless the accountant changes the shares. One entry, same accounts: each expense account is credited at head
/// office and debited to the branches, so no account changes in total and no new account is needed. It is only proposed here: it posts when the
/// owner or accountant approves it, and can be redone later for costs added after.</summary>
public class SharedCostAllocationService : ISharedCostAllocationService
{
    private readonly IApplicationDbContext _context;
    public SharedCostAllocationService(IApplicationDbContext context) => _context = context;

    public async Task<CostAllocationPreviewDto> PreviewAsync(int year, int month, CancellationToken ct = default)
    {
        var (start, end) = Range(year, month);
        var lines = await MonthLinesAsync(start, end, ct);
        var result = new CostAllocationPreviewDto { Year = year, Month = month };

        // Head office = expense lines with no branch, per account, net of what earlier allocations already moved away.
        result.Accounts = lines
            .Where(l => l.Account!.AccountType == AccountType.Expense && EffectiveBranch(l) == null)
            .GroupBy(l => l.AccountId)
            .Select(g => new CostAllocationAccountDto { AccountId = g.Key, AccountCode = g.First().Account!.Code, AccountName = g.First().Account!.NameAr, Amount = Math.Round(g.Sum(l => l.Debit - l.Credit), 2) })
            .Where(a => a.Amount > 0).OrderBy(a => a.AccountCode).ToList();
        result.TotalToAllocate = result.Accounts.Sum(a => a.Amount);

        var names = await _context.Warehouses.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.NameAr, ct);
        var sales = lines.Where(l => l.Account!.AccountType == AccountType.Revenue && EffectiveBranch(l) is not null)
            .GroupBy(l => EffectiveBranch(l)!.Value).Select(g => new { Id = g.Key, Sales = g.Sum(l => l.Credit - l.Debit) }).Where(x => x.Sales > 0).ToList();
        var total = sales.Sum(x => x.Sales);
        result.Branches = sales.Select(x => new CostAllocationBranchDto
        {
            WarehouseId = x.Id, WarehouseName = names.GetValueOrDefault(x.Id, ""), Sales = x.Sales, SharePercent = Math.Round(x.Sales / total * 100, 2)
        }).OrderBy(b => b.WarehouseName).ToList();
        if (result.Branches.Count > 0)
        {
            var diff = 100m - result.Branches.Sum(b => b.SharePercent);
            if (diff != 0) result.Branches.OrderByDescending(b => b.Sales).First().SharePercent += diff;
        }

        if (result.TotalToAllocate <= 0) result.Blocked = "مفيش مصروفات مشتركة لسه موزّعة على الإدارة في الشهر ده.";
        else if (result.Branches.Count == 0) result.Blocked = "مفيش مبيعات متسجلة على فروع في الشهر ده، فمفيش أساس للتوزيع.";
        return result;
    }

    public async Task<Guid> ApplyAsync(ApplyCostAllocationDto dto, string? userName, CancellationToken ct = default)
    {
        var preview = await PreviewAsync(dto.Year, dto.Month, ct);
        if (preview.Blocked is not null) throw new ValidationAppException(preview.Blocked);

        // Not edited: split on the exact sales (no rounded percentages), the same split the branch report works out by itself.
        var edited = dto.Shares is { Count: > 0 };
        var shares = edited ? dto.Shares : preview.Branches.Select(b => new BranchShareDto { WarehouseId = b.WarehouseId, SharePercent = b.Sales }).ToList();
        if (shares.Any(s => s.SharePercent < 0)) throw new ValidationAppException("نسبة الفرع مش ممكن تكون بالسالب.");
        if (shares.GroupBy(s => s.WarehouseId).Any(g => g.Count() > 1)) throw new ValidationAppException("الفرع مكرر في التوزيع.");
        if (edited && Math.Abs(shares.Sum(s => s.SharePercent) - 100m) > 0.01m) throw new ValidationAppException("نسب الفروع لازم مجموعها 100%.");
        var weightTotal = shares.Sum(s => s.SharePercent);
        if (weightTotal <= 0) throw new ValidationAppException("مفيش أساس للتوزيع.");
        var warehouseIds = shares.Select(s => s.WarehouseId).ToList();
        if (await _context.Warehouses.CountAsync(w => warehouseIds.Contains(w.Id) && !w.IsDeleted, ct) != warehouseIds.Count)
            throw new ValidationAppException("في فرع مش موجود في التوزيع.");

        var (_, end) = Range(dto.Year, dto.Month);
        var date = end.AddDays(-1);
        var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, ct)
            ?? throw new ValidationAppException($"مفيش فترة محاسبية بتغطي تاريخ {date:yyyy-MM-dd}.");
        if (period.IsClosed) throw new ValidationAppException($"الفترة المحاسبية ({period.Name}) مقفولة.");

        var lines = new List<JournalEntryLine>();
        var n = 1;
        var label = $"توزيع المصروفات المشتركة - {dto.Year}-{dto.Month:00}";
        foreach (var account in preview.Accounts)
        {
            // each account leaves head office in full and lands on the branches by share; the rounding difference goes to the biggest share
            var parts = shares.Select(s => (s.WarehouseId, Amount: Math.Round(account.Amount * s.SharePercent / weightTotal, 2))).ToList();
            var diff = account.Amount - parts.Sum(p => p.Amount);
            if (diff != 0)
            {
                var i = parts.IndexOf(parts.OrderByDescending(p => p.Amount).First());
                parts[i] = (parts[i].WarehouseId, parts[i].Amount + diff);
            }
            foreach (var (warehouseId, amount) in parts.Where(p => p.Amount != 0))
                lines.Add(new JournalEntryLine { LineNumber = n++, AccountId = account.AccountId, WarehouseId = warehouseId, IsCostAllocation = true, Debit = amount, Credit = 0, Description = label });
            lines.Add(new JournalEntryLine { LineNumber = n++, AccountId = account.AccountId, WarehouseId = null, IsCostAllocation = true, Debit = 0, Credit = account.Amount, Description = label });
        }

        var entry = new JournalEntry
        {
            EntryNumber = $"JV-{(await _context.JournalEntries.CountAsync(ct) + 1):D6}",
            EntryDate = date, FiscalPeriodId = period.Id,
            Description = string.IsNullOrWhiteSpace(dto.Note) ? label : $"{label} - {dto.Note.Trim()}",
            Reference = "COST-ALLOCATION", Status = JournalEntryStatus.Posted, Lines = lines
        };
        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
        return entry.Id;
    }

    private static (DateTime Start, DateTime End) Range(int year, int month)
    {
        if (month < 1 || month > 12 || year < 2000) throw new ValidationAppException("الشهر أو السنة غير صحيحة.");
        var start = new DateTime(year, month, 1);
        return (start, start.AddMonths(1));
    }

    private static Guid? EffectiveBranch(JournalEntryLine l) => l.IsCostAllocation ? l.WarehouseId : l.WarehouseId ?? l.JournalEntry!.WarehouseId;

    private Task<List<JournalEntryLine>> MonthLinesAsync(DateTime start, DateTime end, CancellationToken ct)
        => _context.JournalEntryLines.AsNoTracking().Include(l => l.Account).Include(l => l.JournalEntry)
            .Where(l => (l.JournalEntry!.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Reversed) && !l.JournalEntry.IsDeleted && l.JournalEntry.EntryDate >= start && l.JournalEntry.EntryDate < end
                        && (l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
            .ToListAsync(ct);
}
