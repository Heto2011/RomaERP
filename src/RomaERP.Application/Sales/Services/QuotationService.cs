using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.Services;

public interface IQuotationService
{
    Task<List<QuotationDto>> GetAllAsync(CancellationToken ct = default);
    Task<QuotationDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<QuotationDto> CreateAsync(SaveQuotationDto dto, CancellationToken ct = default);
    Task<QuotationDto> UpdateAsync(Guid id, SaveQuotationDto dto, CancellationToken ct = default);
    Task<QuotationDto> SetStatusAsync(Guid id, QuotationStatus status, CancellationToken ct = default);
    /// <summary>Turns the offer into a sales invoice through the normal invoice service, so every invoice rule still applies.</summary>
    Task<QuotationDto> ConvertAsync(Guid id, ConvertQuotationDto dto, CancellationToken ct = default);
}

/// <summary>Customer offers. They carry no accounting: the books only see them once the customer accepts and the offer becomes an invoice.</summary>
public class QuotationService : IQuotationService
{
    private readonly IApplicationDbContext _c;
    private readonly ISalesService _sales;
    public QuotationService(IApplicationDbContext c, ISalesService sales) { _c = c; _sales = sales; }

    public async Task<List<QuotationDto>> GetAllAsync(CancellationToken ct = default)
        => (await _c.SalesQuotations.AsNoTracking().Include(q => q.Customer).Include(q => q.Lines).Where(q => !q.IsDeleted).OrderByDescending(q => q.QuotationDate).ThenByDescending(q => q.QuotationNumber).Take(500).ToListAsync(ct)).Select(Map).ToList();

    public async Task<QuotationDto> GetAsync(Guid id, CancellationToken ct = default) => Map(await LoadAsync(id, false, ct));

    private async Task<SalesQuotation> LoadAsync(Guid id, bool track, CancellationToken ct)
    {
        var q = _c.SalesQuotations.Include(x => x.Customer).Include(x => x.Lines).Where(x => x.Id == id && !x.IsDeleted);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(SalesQuotation), id);
    }

    public async Task<QuotationDto> CreateAsync(SaveQuotationDto dto, CancellationToken ct = default)
    {
        var q = new SalesQuotation { QuotationNumber = $"QT-{(await _c.SalesQuotations.CountAsync(ct) + 1):D6}" };
        foreach (var l in await FillAsync(q, dto, ct)) q.Lines.Add(l);
        _c.SalesQuotations.Add(q);
        await _c.SaveChangesAsync(ct);
        return await GetAsync(q.Id, ct);
    }

    public async Task<QuotationDto> UpdateAsync(Guid id, SaveQuotationDto dto, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, true, ct);
        if (q.Status is QuotationStatus.Converted or QuotationStatus.Accepted) throw new ValidationAppException("العرض ده اتقبل أو اتحوّل لفاتورة ومينفعش يتعدّل.");
        var newLines = await FillAsync(q, dto, ct);
        _c.SalesQuotationLines.RemoveRange(q.Lines.ToList());
        // New lines are added through their own set: a line added to a tracked quotation's collection with a preset key would be treated as an existing row.
        foreach (var l in newLines) { l.SalesQuotationId = q.Id; _c.SalesQuotationLines.Add(l); }
        await _c.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<List<SalesQuotationLine>> FillAsync(SalesQuotation q, SaveQuotationDto dto, CancellationToken ct)
    {
        if (dto.Lines.Count == 0) throw new ValidationAppException("لازم يكون في بند واحد على الأقل.");
        if (dto.Lines.Any(l => string.IsNullOrWhiteSpace(l.Description))) throw new ValidationAppException("اكتب وصف لكل بند.");
        if (dto.Lines.Any(l => l.Quantity <= 0)) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");
        if (dto.Lines.Any(l => l.UnitPrice < 0)) throw new ValidationAppException("السعر مينفعش يكون بالسالب.");
        if (dto.Lines.Any(l => l.VatRate is < 0 or > 1)) throw new ValidationAppException("نسبة الضريبة لازم تكون بين 0 و100%.");
        if (dto.ValidUntil is { } v && v.Date < dto.QuotationDate.Date) throw new ValidationAppException("تاريخ الصلاحية قبل تاريخ العرض.");
        if (!await _c.Customers.AnyAsync(c => c.Id == dto.CustomerId && !c.IsDeleted, ct)) throw new NotFoundException(nameof(Customer), dto.CustomerId);
        var vat = (await _c.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct))?.VatRate ?? 0;
        q.CustomerId = dto.CustomerId; q.QuotationDate = dto.QuotationDate.Date; q.ValidUntil = dto.ValidUntil?.Date;
        q.Notes = Clean(dto.Notes, 1000); q.Terms = Clean(dto.Terms, 2000);
        var n = 0;
        var built = new List<SalesQuotationLine>();
        foreach (var l in dto.Lines)
        {
            var total = Math.Round(l.Quantity * l.UnitPrice, 2); var rate = l.VatRate ?? vat;
            built.Add(new SalesQuotationLine { LineNumber = ++n, Description = l.Description.Trim(), Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = total, VatRate = rate, VatAmount = Math.Round(total * rate, 2), ItemId = l.ItemId });
        }
        q.SubTotal = built.Sum(l => l.LineTotal);
        q.VatAmount = built.GroupBy(l => l.VatRate).Sum(g => Math.Round(g.Sum(l => l.LineTotal) * g.Key, 2));
        q.TotalAmount = q.SubTotal + q.VatAmount;
        return built;
    }

    private static string? Clean(string? s, int max) { s = s?.Trim(); return string.IsNullOrEmpty(s) ? null : s.Length > max ? s[..max] : s; }

    public async Task<QuotationDto> SetStatusAsync(Guid id, QuotationStatus status, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, true, ct);
        var ok = (q.Status, status) switch
        {
            (QuotationStatus.Draft, QuotationStatus.Sent) => true,
            (QuotationStatus.Draft or QuotationStatus.Sent, QuotationStatus.Accepted or QuotationStatus.Rejected) => true,
            (QuotationStatus.Rejected, QuotationStatus.Draft) => true,
            _ => false,
        };
        if (!ok) throw new ValidationAppException("مينفعش تغيّر حالة العرض بالشكل ده.");
        q.Status = status;
        await _c.SaveChangesAsync(ct);
        return Map(q);
    }

    public async Task<QuotationDto> ConvertAsync(Guid id, ConvertQuotationDto dto, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, true, ct);
        if (q.Status == QuotationStatus.Converted) throw new ValidationAppException("العرض ده اتحوّل لفاتورة قبل كده.");
        if (q.Status == QuotationStatus.Rejected) throw new ValidationAppException("العرض ده مرفوض. رجّعه مسودة الأول لو عايز تحوّله.");
        var invoice = await _sales.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = q.CustomerId, InvoiceDate = dto.InvoiceDate, FiscalPeriodId = dto.FiscalPeriodId, PaymentTerm = dto.PaymentTerm, WarehouseId = dto.WarehouseId,
            Notes = $"من عرض السعر {q.QuotationNumber}",
            Lines = q.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesInvoiceLineInputDto { Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatRate = l.VatRate, ItemId = l.ItemId }).ToList(),
        }, ct);
        q.Status = QuotationStatus.Converted;
        q.ConvertedInvoiceId = invoice.Id;
        await _c.SaveChangesAsync(ct);
        return Map(q);
    }

    private static QuotationDto Map(SalesQuotation q) => new()
    {
        Id = q.Id, QuotationNumber = q.QuotationNumber, QuotationDate = q.QuotationDate, ValidUntil = q.ValidUntil,
        IsExpired = q.ValidUntil is { } v && v.Date < DateTime.UtcNow.Date && (q.Status == QuotationStatus.Draft || q.Status == QuotationStatus.Sent),
        CustomerId = q.CustomerId, CustomerName = q.Customer?.NameAr ?? string.Empty, Status = q.Status, Notes = q.Notes, Terms = q.Terms,
        SubTotal = q.SubTotal, VatAmount = q.VatAmount, TotalAmount = q.TotalAmount, ConvertedInvoiceId = q.ConvertedInvoiceId,
        Lines = q.Lines.OrderBy(l => l.LineNumber).Select(l => new QuotationLineDto { LineNumber = l.LineNumber, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = l.LineTotal, VatRate = l.VatRate, VatAmount = l.VatAmount, ItemId = l.ItemId }).ToList(),
    };
}
