using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Purchasing;

namespace RomaERP.Application.Accounting.Services;

public interface IReviewService
{
    Task<List<ReconciliationCheckDto>> GetReconciliationAsync(CancellationToken ct = default);
    Task<List<CloseCheckDto>> GetCloseChecklistAsync(int year, int month, CancellationToken ct = default);
    Task<List<ApprovalLogDto>> GetApprovalsLogAsync(CancellationToken ct = default);
    Task<List<UnusualEntryDto>> GetUnusualEntriesAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<NumberGapDto>> GetNumberGapsAsync(CancellationToken ct = default);
}

/// <summary>Read-only checks for the accountant or auditor: do the books agree with the detail behind them, is the month ready to close, who approved
/// what, which entries stand out, and where is a number missing from a sequence. It changes nothing.</summary>
public class ReviewService : IReviewService
{
    private const decimal Tolerance = 1.00m;   // rounding across many lines
    private readonly IApplicationDbContext _context;
    private readonly ISharedCostAllocationService _allocation;

    public ReviewService(IApplicationDbContext context, ISharedCostAllocationService allocation)
    {
        _context = context;
        _allocation = allocation;
    }

    private IQueryable<JournalEntryLine> BookLines() => _context.JournalEntryLines.AsNoTracking()
        .Where(l => !l.JournalEntry!.IsDeleted && (l.JournalEntry.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Reversed));

    // ---------- 1. ledger against the detail behind it ----------
    public async Task<List<ReconciliationCheckDto>> GetReconciliationAsync(CancellationToken ct = default)
    {
        var byCode = await BookLines().GroupBy(l => l.Account!.Code).Select(g => new { Code = g.Key, Net = g.Sum(l => l.Debit - l.Credit) }).ToDictionaryAsync(x => x.Code, x => x.Net, ct);
        decimal Net(string code) => byCode.GetValueOrDefault(code);

        var totals = await BookLines().GroupBy(_ => 1).Select(g => new { Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) }).FirstOrDefaultAsync(ct);
        var ar = await _context.Customers.AsNoTracking().SumAsync(c => (decimal?)c.ArBalance, ct) ?? 0m;
        var ap = await _context.Vendors.AsNoTracking().SumAsync(v => (decimal?)v.ApBalance, ct) ?? 0m;
        var stock = (await _context.Items.AsNoTracking().Select(i => new { i.QuantityOnHand, i.AverageCost }).ToListAsync(ct)).Sum(i => i.QuantityOnHand * i.AverageCost);
        var custody = await _context.Employees.AsNoTracking().SumAsync(e => (decimal?)e.CustodyBalance, ct) ?? 0m;
        var debts = await _context.EmployeeDebts.AsNoTracking().Where(d => d.Status == EmployeeDebtStatus.Active && d.Reason != EmployeeDebtReason.InternalPenalty).SumAsync(d => (decimal?)d.RemainingAmount, ct) ?? 0m;

        ReconciliationCheckDto Check(string key, string ledger, string detail, decimal l, decimal d) => new()
        {
            Key = key, LedgerLabel = ledger, DetailLabel = detail, LedgerBalance = Math.Round(l, 2), DetailBalance = Math.Round(d, 2),
            Difference = Math.Round(l - d, 2), Ok = Math.Abs(l - d) <= Tolerance
        };
        return new List<ReconciliationCheckDto>
        {
            Check("trial", "Debit", "Credit", totals?.Debit ?? 0, totals?.Credit ?? 0),
            Check("ar", "1120", "Customers", Net(AccountingConstants.AccountsReceivableAccountCode), ar),
            Check("ap", "2120", "Vendors", -Net(AccountingConstants.AccountsPayableAccountCode), ap),
            Check("inventory", "1160", "Stock", Net(AccountingConstants.InventoryAccountCode), stock),
            Check("custody", "1170", "Custody+Debts", Net(AccountingConstants.EmployeeCustodyAccountCode), custody + debts)
        };
    }

    // ---------- 2. month-end checklist ----------
    public async Task<List<CloseCheckDto>> GetCloseChecklistAsync(int year, int month, CancellationToken ct = default)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var list = new List<CloseCheckDto>();
        void Add(string key, int count, string link, decimal amount = 0) => list.Add(new CloseCheckDto { Key = key, Count = count, Ok = count == 0, Link = link, Amount = amount });

        Add("drafts", await _context.JournalEntries.CountAsync(e => !e.IsDeleted && e.Status == JournalEntryStatus.Draft && e.EntryDate >= start && e.EntryDate < end, ct), "/accounting/journal-entries");
        Add("counts", await _context.PhysicalStockCounts.CountAsync(c => !c.IsDeleted && c.Status == 1, ct), "/inventory/physical-stock-counts");
        Add("pendingPurchases", await _context.PendingPurchaseInvoices.CountAsync(p => !p.IsDeleted && p.Status == PendingPurchaseStatus.Pending, ct), "/purchasing/invoices");
        Add("payroll", await _context.PayrollRuns.CountAsync(r => !r.IsDeleted && r.RunDate >= start && r.RunDate < end && r.Status != PayrollRunStatus.Posted, ct), "/hr/payroll");
        Add("transfers", await _context.StockTransfers.CountAsync(t => !t.IsDeleted && t.Status == StockTransferStatus.InTransit, ct), "/inventory/transfers");

        var hasAssets = await _context.FixedAssets.AnyAsync(a => !a.IsDeleted, ct);
        var depreciated = !hasAssets || await _context.DepreciationRuns.AnyAsync(r => !r.IsDeleted && r.Status == DepreciationRunStatus.Posted && r.RunDate >= start && r.RunDate < end, ct);
        Add("depreciation", depreciated ? 0 : 1, "/accounting/depreciation-runs");

        var shared = await _allocation.PreviewAsync(year, month, ct);
        list.Add(new CloseCheckDto { Key = "sharedCosts", Ok = shared.TotalToAllocate <= 0, Count = shared.Accounts.Count, Amount = shared.TotalToAllocate, Link = "/accounting/cost-allocation" });

        var trial = (await GetReconciliationAsync(ct)).First(c => c.Key == "trial");
        list.Add(new CloseCheckDto { Key = "trialBalanced", Ok = trial.Ok, Count = trial.Ok ? 0 : 1, Amount = trial.Difference, Link = "/accounting/trial-balance" });
        return list;
    }

    // ---------- 3. who approved what ----------
    public async Task<List<ApprovalLogDto>> GetApprovalsLogAsync(CancellationToken ct = default)
    {
        var log = new List<ApprovalLogDto>();

        var counts = await _context.PhysicalStockCounts.AsNoTracking().Include(c => c.Item).Include(c => c.Warehouse)
            .Where(c => !c.IsDeleted && (c.Status == 2 || c.Status == 3) && c.DecidedAt != null).ToListAsync(ct);
        log.AddRange(counts.Select(c => new ApprovalLogDto
        {
            Kind = "StockCount", Decision = c.Status == 3 ? "Rejected" : "Approved", By = c.DecidedBy, AtUtc = c.DecidedAt!.Value, Note = c.DecisionNote,
            Detail = $"{c.Item?.Code} {c.Item?.NameAr} - {c.Warehouse?.NameAr}: {c.SystemQuantity:0.##} → {c.CountedQuantity:0.##}", Amount = Math.Round((c.CountedQuantity - c.SystemQuantity) * c.UnitCost, 2)
        }));

        var deductions = await _context.EmployeeDebtDeductions.AsNoTracking().Include(d => d.EmployeeDebt).ThenInclude(d => d!.Employee).Where(d => !d.IsDeleted).ToListAsync(ct);
        log.AddRange(deductions.Select(d => new ApprovalLogDto
        {
            Kind = "PayrollDebtDeduction", Decision = "Approved", By = d.ApprovedBy, AtUtc = d.CreatedAtUtc, Amount = d.Amount,
            Detail = $"{d.EmployeeDebt?.Employee?.FullNameAr} - {d.EmployeeDebt?.Description}"
        }));

        var purchases = await _context.PendingPurchaseInvoices.AsNoTracking().Where(p => !p.IsDeleted && p.Status != PendingPurchaseStatus.Pending && p.DecidedAtUtc != null).ToListAsync(ct);
        log.AddRange(purchases.Select(p => new ApprovalLogDto
        {
            Kind = "PendingPurchase", Decision = p.Status == PendingPurchaseStatus.Rejected ? "Rejected" : "Approved", By = p.DecidedByName, AtUtc = p.DecidedAtUtc!.Value,
            Amount = p.SubTotal, Detail = $"{p.VendorName} ({p.RequestedByName})", Note = p.RejectionReason
        }));

        var allocations = await _context.JournalEntries.AsNoTracking().Include(e => e.Lines).Where(e => !e.IsDeleted && e.Reference == "COST-ALLOCATION").ToListAsync(ct);
        log.AddRange(allocations.Select(e => new ApprovalLogDto
        {
            Kind = "CostAllocation", Decision = "Approved", By = e.CreatedBy, AtUtc = e.CreatedAtUtc, Detail = e.Description ?? e.EntryNumber, Amount = e.Lines.Where(l => l.WarehouseId == null).Sum(l => l.Credit)
        }));

        return log.OrderByDescending(l => l.AtUtc).Take(300).ToList();
    }

    // ---------- 4. entries that stand out ----------
    public async Task<List<UnusualEntryDto>> GetUnusualEntriesAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var start = (from ?? DateTime.UtcNow.AddDays(-90)).Date;
        var end = (to ?? DateTime.UtcNow).Date.AddDays(1);
        var entries = await _context.JournalEntries.AsNoTracking().Include(e => e.Lines).ThenInclude(l => l.Account)
            .Where(e => !e.IsDeleted && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed) && e.EntryDate >= start && e.EntryDate < end).ToListAsync(ct);
        if (entries.Count == 0) return new();

        var totals = entries.ToDictionary(e => e.Id, e => e.Lines.Sum(l => l.Debit));
        var mean = totals.Values.Average();
        var std = (decimal)Math.Sqrt(totals.Values.Select(v => Math.Pow((double)(v - mean), 2)).Average());
        var bigThreshold = entries.Count >= 20 ? mean + 3 * std : decimal.MaxValue;

        var result = new List<UnusualEntryDto>();
        foreach (var e in entries)
        {
            var total = totals[e.Id];
            var reasons = new List<string>();
            if (total >= 10_000m && total % 1000m == 0) reasons.Add("Round");
            if (total > bigThreshold) reasons.Add("Large");
            if ((e.CreatedAtUtc.Date - e.EntryDate.Date).TotalDays > 30) reasons.Add("Backdated");
            if (e.Status == JournalEntryStatus.Reversed || (e.Description?.StartsWith("عكس قيد") ?? false)) reasons.Add("Reversal");
            if (e.Reference != "OPENING-BALANCE" && e.Lines.Any(l => l.Account?.AccountType == AccountType.Equity)) reasons.Add("Equity");
            if (reasons.Count == 0) continue;
            result.Add(new UnusualEntryDto { EntryId = e.Id, EntryNumber = e.EntryNumber, EntryDate = e.EntryDate, CreatedAtUtc = e.CreatedAtUtc, CreatedBy = e.CreatedBy, Description = e.Description, Total = total, Reasons = reasons });
        }
        return result.OrderByDescending(r => r.EntryDate).ThenByDescending(r => r.Total).Take(200).ToList();
    }

    // ---------- 5. numbers missing from a sequence ----------
    public async Task<List<NumberGapDto>> GetNumberGapsAsync(CancellationToken ct = default)
    {
        var result = new List<NumberGapDto>();
        async Task Series(string name, IQueryable<string> numbers)
        {
            var all = await numbers.ToListAsync(ct);
            foreach (var group in all.Select(n => Regex.Match(n ?? "", @"^(.*?)(\d+)$")).Where(m => m.Success).GroupBy(m => m.Groups[1].Value))
            {
                var set = group.Select(m => long.Parse(m.Groups[2].Value)).Distinct().OrderBy(n => n).ToList();
                var missing = new List<long>();
                var missingCount = 0;
                for (var n = set[0]; n <= set[^1]; n++)
                    if (!set.Contains(n)) { missingCount++; if (missing.Count < 100) missing.Add(n); }
                result.Add(new NumberGapDto { Series = $"{name} {group.Key}".Trim(), Count = set.Count, First = set[0], Last = set[^1], MissingCount = missingCount, Missing = missing });
            }
        }
        await Series("SalesInvoice", _context.SalesInvoices.AsNoTracking().Select(i => i.InvoiceNumber));
        await Series("PurchaseInvoice", _context.PurchaseInvoices.AsNoTracking().Select(i => i.InvoiceNumber));
        await Series("JournalEntry", _context.JournalEntries.AsNoTracking().Select(e => e.EntryNumber));
        return result;
    }
}
