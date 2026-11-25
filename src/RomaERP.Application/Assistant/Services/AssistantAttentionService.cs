using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Application.Assistant.Services;

/// <summary>One thing that needs the person's attention today, with the question that opens it in the assistant.</summary>
public record AttentionItemDto(string Key, string Label, int Count, decimal? Amount, string Question);

public interface IAssistantAttentionService
{
    Task<List<AttentionItemDto>> GetAsync(CancellationToken ct = default);
}

/// <summary>What needs attention today. It is counted straight from the books (no AI, no cost) and shows each person only what their role may open.</summary>
public class AssistantAttentionService : IAssistantAttentionService
{
    public const int OverdueDays = 30;
    private readonly IApplicationDbContext _c;
    private readonly ICurrentUserService _user;
    public AssistantAttentionService(IApplicationDbContext c, ICurrentUserService user) { _c = c; _user = user; }

    private bool Is(params string[] roles) => roles.Any(r => _user.Roles.Contains(r, StringComparer.OrdinalIgnoreCase));

    public async Task<List<AttentionItemDto>> GetAsync(CancellationToken ct = default)
    {
        var items = new List<AttentionItemDto>();
        var cutoff = DateTime.UtcNow.Date.AddDays(-OverdueDays);
        if (Is("Admin", "Accountant"))
        {
            var ar = await _c.SalesInvoices.AsNoTracking().Where(i => !i.IsDeleted && i.PaidAmount < i.TotalAmount && i.InvoiceDate < cutoff).Select(i => i.TotalAmount - i.PaidAmount).ToListAsync(ct);
            if (ar.Count > 0) items.Add(new("overdue-ar", $"فواتير عملاء متأخرة أكتر من {OverdueDays} يوم", ar.Count, ar.Sum(), "إيه الفواتير المتأخرة على العملاء وكام اللي علينا نحصّله؟"));
            var ap = await _c.PurchaseInvoices.AsNoTracking().Where(i => !i.IsDeleted && i.PaidAmount < i.TotalAmount && i.InvoiceDate < cutoff).Select(i => i.TotalAmount - i.PaidAmount).ToListAsync(ct);
            if (ap.Count > 0) items.Add(new("overdue-ap", $"فواتير موردين متأخرة أكتر من {OverdueDays} يوم", ap.Count, ap.Sum(), "إيه الفواتير اللي علينا للموردين ومتأخرة؟"));
            var bank = await _c.BankFeedTransactions.AsNoTracking().Where(b => !b.IsDeleted && !b.IsMatched).Select(b => Math.Abs(b.Amount)).ToListAsync(ct);
            if (bank.Count > 0) items.Add(new("bank-unmatched", "حركات بنك مرفوعة ومش متطابقة مع الدفاتر", bank.Count, bank.Sum(), "إيه حركات البنك اللي لسه مش متطابقة؟"));
            var rec = await _c.RecurringInvoiceDues.AsNoTracking().CountAsync(d => !d.IsDeleted && d.Status == RomaERP.Domain.Sales.RecurringDueStatus.Pending, ct);
            if (rec > 0) items.Add(new("recurring-invoices", "فواتير متكررة مستنية إصدارك", rec, null, "إيه الفواتير المتكررة المستنية؟"));
            var low = await _c.Items.AsNoTracking().CountAsync(i => !i.IsDeleted && i.IsActive && i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel, ct);
            if (low > 0) items.Add(new("low-stock", "أصناف وصلت لحد إعادة الطلب", low, null, "إيه الأصناف اللي قربت تخلص؟"));
        }
        if (Is("Admin"))
        {
            var exp = await _c.ExpenseCaptures.AsNoTracking().Where(e => !e.IsDeleted && e.Status == RomaERP.Domain.Assistant.ExpenseCaptureStatus.PendingApproval).Select(e => e.Amount ?? 0).ToListAsync(ct);
            if (exp.Count > 0) items.Add(new("expense-approvals", "مصروفات مستنية اعتمادك", exp.Count, exp.Sum(), "إيه المصروفات المنتظرة اعتمادي؟"));
        }
        if (Is("Admin", "HR"))
        {
            var req = await _c.EmployeeRequests.AsNoTracking().CountAsync(r => !r.IsDeleted && r.Status == RomaERP.Domain.HR.EmployeeRequestStatus.Pending, ct);
            if (req > 0) items.Add(new("employee-requests", "طلبات موظفين منتظرة قرار", req, null, "إيه طلبات الموظفين المنتظرة؟"));
        }
        return items;
    }
}
