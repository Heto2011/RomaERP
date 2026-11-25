using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.Services;

public interface IRecurringInvoiceService
{
    Task<List<RecurringInvoiceDto>> GetAllAsync(CancellationToken ct = default);
    Task<RecurringInvoiceDto> SaveAsync(Guid? id, SaveRecurringInvoiceDto dto, CancellationToken ct = default);
    Task<RecurringInvoiceDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task<List<RecurringDueDto>> GetPendingAsync(CancellationToken ct = default);
    /// <summary>Prepares the entries that have come due (one per recurring invoice per period, never twice). Nothing is invoiced. Returns how many were prepared.</summary>
    Task<int> PrepareDueAsync(DateTime today, CancellationToken ct = default);
    /// <summary>A person confirms an entry: the real invoice is made through the normal invoice service, so every invoice rule still applies.</summary>
    Task<RecurringDueDto> IssueAsync(Guid dueId, IssueRecurringDto dto, string? userName, CancellationToken ct = default);
    Task<RecurringDueDto> SkipAsync(Guid dueId, string? userName, CancellationToken ct = default);
}

/// <summary>Invoices that repeat. The system only prepares them; a person issues or skips each one (nothing automatic reaches the books).</summary>
public class RecurringInvoiceService : IRecurringInvoiceService
{
    private static readonly int[] Intervals = { 1, 3, 6, 12 };
    private readonly IApplicationDbContext _c;
    private readonly ISalesService _sales;
    public RecurringInvoiceService(IApplicationDbContext c, ISalesService sales) { _c = c; _sales = sales; }

    public async Task<List<RecurringInvoiceDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _c.RecurringInvoices.AsNoTracking().Include(r => r.Customer).Include(r => r.Lines).Include(r => r.Dues).Where(r => !r.IsDeleted).OrderBy(r => r.Title).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<RecurringInvoiceDto> SaveAsync(Guid? id, SaveRecurringInvoiceDto dto, CancellationToken ct = default)
    {
        var title = dto.Title?.Trim();
        if (string.IsNullOrEmpty(title)) throw new ValidationAppException("اكتب اسم الفاتورة المتكررة (مثلًا: اشتراك شهري).");
        if (title.Length > 200) throw new ValidationAppException("الاسم أطول من المسموح.");
        if (!Intervals.Contains(dto.IntervalMonths)) throw new ValidationAppException("التكرار لازم يكون كل شهر أو 3 أو 6 أو 12 شهر.");
        if (dto.DayOfMonth is < 1 or > 31) throw new ValidationAppException("يوم الشهر لازم يكون من 1 لـ 31 (والشهر الأقصر بيتحسب آخر يوم فيه).");
        if (dto.EndDate is { } e && e.Date < dto.StartDate.Date) throw new ValidationAppException("تاريخ النهاية قبل تاريخ البداية.");
        if (dto.PaymentTerm is not (PaymentTerm.Cash or PaymentTerm.Card or PaymentTerm.Credit)) throw new ValidationAppException("طريقة الدفع: نقدي أو كارت أو آجل.");
        ValidateLines(dto.Lines);
        if (!await _c.Customers.AnyAsync(x => x.Id == dto.CustomerId && !x.IsDeleted, ct)) throw new NotFoundException(nameof(Customer), dto.CustomerId);

        RecurringInvoice r;
        if (id is { } existing)
        {
            r = await _c.RecurringInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == existing && !x.IsDeleted, ct) ?? throw new NotFoundException(nameof(RecurringInvoice), existing);
            _c.RecurringInvoiceLines.RemoveRange(r.Lines.ToList());
        }
        else { r = new RecurringInvoice(); _c.RecurringInvoices.Add(r); }
        r.Title = title; r.CustomerId = dto.CustomerId; r.IntervalMonths = dto.IntervalMonths; r.DayOfMonth = dto.DayOfMonth;
        r.StartDate = dto.StartDate.Date; r.EndDate = dto.EndDate?.Date; r.PaymentTerm = dto.PaymentTerm;
        var notes = dto.Notes?.Trim(); r.Notes = string.IsNullOrEmpty(notes) ? null : notes.Length > 1000 ? notes[..1000] : notes;
        var n = 0;
        foreach (var l in dto.Lines)
        {
            var line = new RecurringInvoiceLine { LineNumber = ++n, Description = l.Description.Trim(), Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatRate = l.VatRate, RecurringInvoiceId = r.Id };
            // Added through their own set so a preset key is never mistaken for an existing row.
            _c.RecurringInvoiceLines.Add(line);
        }
        await _c.SaveChangesAsync(ct);
        return (await GetAllAsync(ct)).First(x => x.Id == r.Id);
    }

    private static void ValidateLines(List<RecurringLineInputDto> lines)
    {
        if (lines.Count == 0) throw new ValidationAppException("لازم يكون في بند واحد على الأقل.");
        if (lines.Any(l => string.IsNullOrWhiteSpace(l.Description))) throw new ValidationAppException("اكتب وصف لكل بند.");
        if (lines.Any(l => l.Quantity <= 0)) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");
        if (lines.Any(l => l.UnitPrice < 0)) throw new ValidationAppException("السعر مينفعش يكون بالسالب.");
        if (lines.Any(l => l.VatRate is < 0 or > 1)) throw new ValidationAppException("نسبة الضريبة لازم تكون بين 0 و100%.");
    }

    public async Task<RecurringInvoiceDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var r = await _c.RecurringInvoices.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException(nameof(RecurringInvoice), id);
        r.IsActive = active;
        await _c.SaveChangesAsync(ct);
        return (await GetAllAsync(ct)).First(x => x.Id == id);
    }

    public async Task<List<RecurringDueDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var dues = await _c.RecurringInvoiceDues.AsNoTracking().Include(d => d.RecurringInvoice!).ThenInclude(r => r.Customer).Include(d => d.RecurringInvoice!).ThenInclude(r => r.Lines)
            .Where(d => !d.IsDeleted && d.Status == RecurringDueStatus.Pending).OrderBy(d => d.DueDate).ToListAsync(ct);
        return dues.Select(MapDue).ToList();
    }

    /// <summary>The date a period falls due: the chosen day, or the last day of a shorter month.</summary>
    public static DateTime DueDateIn(int year, int month, int day) => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    public async Task<int> PrepareDueAsync(DateTime today, CancellationToken ct = default)
    {
        today = today.Date;
        var items = await _c.RecurringInvoices.Include(r => r.Dues).Where(r => !r.IsDeleted && r.IsActive && r.StartDate <= today).ToListAsync(ct);
        var prepared = 0;
        foreach (var r in items)
        {
            if (r.EndDate is { } end && end.Date < new DateTime(today.Year, today.Month, 1)) continue;
            var monthsSinceStart = (today.Year - r.StartDate.Year) * 12 + today.Month - r.StartDate.Month;
            if (monthsSinceStart < 0 || monthsSinceStart % r.IntervalMonths != 0) continue;
            var due = DueDateIn(today.Year, today.Month, r.DayOfMonth);
            if (due < r.StartDate.Date) due = r.StartDate.Date;
            if (r.EndDate is { } e2 && due > e2.Date) continue;
            if (today < due) continue;
            var key = $"{today.Year:D4}-{today.Month:D2}";
            if (r.Dues.Any(d => d.PeriodKey == key)) continue;
            _c.RecurringInvoiceDues.Add(new RecurringInvoiceDue { RecurringInvoiceId = r.Id, DueDate = due, PeriodKey = key, Status = RecurringDueStatus.Pending });
            prepared++;
        }
        if (prepared > 0) await _c.SaveChangesAsync(ct);
        return prepared;
    }

    private async Task<RecurringInvoiceDue> LoadPendingAsync(Guid id, CancellationToken ct)
    {
        var d = await _c.RecurringInvoiceDues.Include(x => x.RecurringInvoice!).ThenInclude(r => r.Customer).Include(x => x.RecurringInvoice!).ThenInclude(r => r.Lines).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(RecurringInvoiceDue), id);
        if (d.Status != RecurringDueStatus.Pending) throw new ValidationAppException("الفاتورة دي اتصدّرت أو اتخطّت قبل كده.");
        return d;
    }

    public async Task<RecurringDueDto> IssueAsync(Guid dueId, IssueRecurringDto dto, string? userName, CancellationToken ct = default)
    {
        var d = await LoadPendingAsync(dueId, ct);
        var r = d.RecurringInvoice!;
        var lines = dto.Lines is { Count: > 0 } ? dto.Lines : r.Lines.OrderBy(l => l.LineNumber).Select(l => new RecurringLineInputDto { Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatRate = l.VatRate }).ToList();
        ValidateLines(lines);
        var invoice = await _sales.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = r.CustomerId, InvoiceDate = dto.InvoiceDate, FiscalPeriodId = dto.FiscalPeriodId, PaymentTerm = r.PaymentTerm,
            Notes = $"{r.Title} — {d.PeriodKey}" + (string.IsNullOrWhiteSpace(r.Notes) ? "" : $" — {r.Notes}"),
            Lines = lines.Select(l => new SalesInvoiceLineInputDto { Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatRate = l.VatRate }).ToList(),
        }, ct);
        d.Status = RecurringDueStatus.Issued; d.InvoiceId = invoice.Id; d.DecidedAtUtc = DateTime.UtcNow; d.DecidedBy = userName;
        await _c.SaveChangesAsync(ct);
        return MapDue(d);
    }

    public async Task<RecurringDueDto> SkipAsync(Guid dueId, string? userName, CancellationToken ct = default)
    {
        var d = await LoadPendingAsync(dueId, ct);
        d.Status = RecurringDueStatus.Skipped; d.DecidedAtUtc = DateTime.UtcNow; d.DecidedBy = userName;
        await _c.SaveChangesAsync(ct);
        return MapDue(d);
    }

    private static List<RecurringLineInputDto> Lines(IEnumerable<RecurringInvoiceLine> l) => l.OrderBy(x => x.LineNumber).Select(x => new RecurringLineInputDto { Description = x.Description, Quantity = x.Quantity, UnitPrice = x.UnitPrice, VatRate = x.VatRate }).ToList();
    private static decimal Net(IEnumerable<RecurringInvoiceLine> l) => l.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2));

    private static RecurringInvoiceDto Map(RecurringInvoice r) => new()
    {
        Id = r.Id, Title = r.Title, CustomerId = r.CustomerId, CustomerName = r.Customer?.NameAr ?? string.Empty, IntervalMonths = r.IntervalMonths, DayOfMonth = r.DayOfMonth,
        StartDate = r.StartDate, EndDate = r.EndDate, PaymentTerm = r.PaymentTerm, Notes = r.Notes, IsActive = r.IsActive, Total = Net(r.Lines),
        PendingCount = r.Dues.Count(d => !d.IsDeleted && d.Status == RecurringDueStatus.Pending), Lines = Lines(r.Lines),
    };

    private static RecurringDueDto MapDue(RecurringInvoiceDue d) => new()
    {
        Id = d.Id, RecurringInvoiceId = d.RecurringInvoiceId, Title = d.RecurringInvoice?.Title ?? string.Empty, CustomerName = d.RecurringInvoice?.Customer?.NameAr ?? string.Empty,
        DueDate = d.DueDate, PeriodKey = d.PeriodKey, Status = d.Status, PaymentTerm = d.RecurringInvoice?.PaymentTerm ?? PaymentTerm.Credit, InvoiceId = d.InvoiceId,
        Total = d.RecurringInvoice is null ? 0 : Net(d.RecurringInvoice.Lines), Lines = d.RecurringInvoice is null ? new() : Lines(d.RecurringInvoice.Lines),
    };
}
