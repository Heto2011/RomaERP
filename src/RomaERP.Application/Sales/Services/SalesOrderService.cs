using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.Services;

public interface ISalesOrderService
{
    Task<List<SalesOrderDto>> GetAllAsync(CancellationToken ct = default);
    Task<SalesOrderDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<SalesOrderDto> CreateAsync(SaveSalesOrderDto dto, CancellationToken ct = default);
    /// <summary>Starts an order from a quotation (the customer said yes): the same customer and lines, still to be confirmed.</summary>
    Task<SalesOrderDto> CreateFromQuotationAsync(Guid quotationId, CancellationToken ct = default);
    Task<SalesOrderDto> UpdateAsync(Guid id, SaveSalesOrderDto dto, CancellationToken ct = default);
    Task<SalesOrderDto> ConfirmAsync(Guid id, CancellationToken ct = default);
    Task<SalesOrderDto> MarkDeliveredAsync(Guid id, string? userName, CancellationToken ct = default);
    Task<SalesOrderDto> CancelAsync(Guid id, CancellationToken ct = default);
    Task<SalesOrderDto> ConvertAsync(Guid id, ConvertSalesOrderDto dto, CancellationToken ct = default);
}

/// <summary>Customer orders and their delivery. No accounting and no stock until the invoice (made through the normal invoice service, which issues the stock).</summary>
public class SalesOrderService : ISalesOrderService
{
    private readonly IApplicationDbContext _c;
    private readonly ISalesService _sales;
    public SalesOrderService(IApplicationDbContext c, ISalesService sales) { _c = c; _sales = sales; }

    public async Task<List<SalesOrderDto>> GetAllAsync(CancellationToken ct = default)
        => (await _c.SalesOrders.AsNoTracking().Include(o => o.Customer).Include(o => o.Lines).Where(o => !o.IsDeleted).OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderNumber).Take(500).ToListAsync(ct)).Select(Map).ToList();

    public async Task<SalesOrderDto> GetAsync(Guid id, CancellationToken ct = default) => Map(await LoadAsync(id, false, ct));

    private async Task<SalesOrder> LoadAsync(Guid id, bool track, CancellationToken ct)
    {
        var q = _c.SalesOrders.Include(x => x.Customer).Include(x => x.Lines).Where(x => x.Id == id && !x.IsDeleted);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(SalesOrder), id);
    }

    public async Task<SalesOrderDto> CreateAsync(SaveSalesOrderDto dto, CancellationToken ct = default)
    {
        var o = new SalesOrder { OrderNumber = $"SO-{(await _c.SalesOrders.CountAsync(ct) + 1):D6}", QuotationId = dto.QuotationId };
        foreach (var l in await FillAsync(o, dto, ct)) o.Lines.Add(l);
        _c.SalesOrders.Add(o);
        await _c.SaveChangesAsync(ct);
        return await GetAsync(o.Id, ct);
    }

    public async Task<SalesOrderDto> CreateFromQuotationAsync(Guid quotationId, CancellationToken ct = default)
    {
        var q = await _c.SalesQuotations.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == quotationId && !x.IsDeleted, ct) ?? throw new NotFoundException(nameof(SalesQuotation), quotationId);
        if (q.Status is QuotationStatus.Rejected or QuotationStatus.Converted) throw new ValidationAppException("العرض ده مرفوض أو اتحوّل لفاتورة، فمينفعش يتعمل منه أمر بيع.");
        if (await _c.SalesOrders.AnyAsync(o => o.QuotationId == quotationId && !o.IsDeleted && o.Status != SalesOrderStatus.Cancelled, ct)) throw new ValidationAppException("اتعمل أمر بيع من العرض ده قبل كده.");
        return await CreateAsync(new SaveSalesOrderDto
        {
            CustomerId = q.CustomerId, OrderDate = DateTime.UtcNow.Date, QuotationId = q.Id, Notes = $"من عرض السعر {q.QuotationNumber}",
            Lines = q.Lines.OrderBy(l => l.LineNumber).Select(l => new QuotationLineInputDto { Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatRate = l.VatRate, ItemId = l.ItemId }).ToList(),
        }, ct);
    }

    public async Task<SalesOrderDto> UpdateAsync(Guid id, SaveSalesOrderDto dto, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status != SalesOrderStatus.Draft) throw new ValidationAppException("الأمر ده اتأكد أو اتقفل ومينفعش يتعدّل.");
        var newLines = await FillAsync(o, dto, ct);
        _c.SalesOrderLines.RemoveRange(o.Lines.ToList());
        // New lines are added through their own set: a line added to a tracked order's collection with a preset key would be treated as an existing row.
        foreach (var l in newLines) { l.SalesOrderId = o.Id; _c.SalesOrderLines.Add(l); }
        await _c.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<List<SalesOrderLine>> FillAsync(SalesOrder o, SaveSalesOrderDto dto, CancellationToken ct)
    {
        if (dto.Lines.Count == 0) throw new ValidationAppException("لازم يكون في بند واحد على الأقل.");
        if (dto.Lines.Any(l => string.IsNullOrWhiteSpace(l.Description))) throw new ValidationAppException("اكتب وصف لكل بند.");
        if (dto.Lines.Any(l => l.Quantity <= 0)) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");
        if (dto.Lines.Any(l => l.UnitPrice < 0)) throw new ValidationAppException("السعر مينفعش يكون بالسالب.");
        if (dto.Lines.Any(l => l.VatRate is < 0 or > 1)) throw new ValidationAppException("نسبة الضريبة لازم تكون بين 0 و100%.");
        if (dto.DeliveryDate is { } d && d.Date < dto.OrderDate.Date) throw new ValidationAppException("تاريخ التسليم قبل تاريخ الأمر.");
        if (!await _c.Customers.AnyAsync(c => c.Id == dto.CustomerId && !c.IsDeleted, ct)) throw new NotFoundException(nameof(Customer), dto.CustomerId);
        var vat = (await _c.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct))?.VatRate ?? 0;
        string? Clean(string? s, int max) { s = s?.Trim(); return string.IsNullOrEmpty(s) ? null : s.Length > max ? s[..max] : s; }
        o.CustomerId = dto.CustomerId; o.OrderDate = dto.OrderDate.Date; o.DeliveryDate = dto.DeliveryDate?.Date; o.DeliveryAddress = Clean(dto.DeliveryAddress, 300); o.Notes = Clean(dto.Notes, 1000);
        var n = 0; var built = new List<SalesOrderLine>();
        foreach (var l in dto.Lines)
        {
            var total = Math.Round(l.Quantity * l.UnitPrice, 2); var rate = l.VatRate ?? vat;
            built.Add(new SalesOrderLine { LineNumber = ++n, Description = l.Description.Trim(), Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = total, VatRate = rate, VatAmount = Math.Round(total * rate, 2), ItemId = l.ItemId });
        }
        o.SubTotal = built.Sum(l => l.LineTotal);
        o.VatAmount = built.GroupBy(l => l.VatRate).Sum(g => Math.Round(g.Sum(l => l.LineTotal) * g.Key, 2));
        o.TotalAmount = o.SubTotal + o.VatAmount;
        return built;
    }

    public async Task<SalesOrderDto> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status != SalesOrderStatus.Draft) throw new ValidationAppException("الأمر ده مش مسودة.");
        o.Status = SalesOrderStatus.Confirmed; await _c.SaveChangesAsync(ct); return Map(o);
    }

    public async Task<SalesOrderDto> MarkDeliveredAsync(Guid id, string? userName, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status != SalesOrderStatus.Confirmed) throw new ValidationAppException("لازم الأمر يكون مؤكد الأول.");
        o.Status = SalesOrderStatus.Delivered; o.DeliveredAtUtc = DateTime.UtcNow; o.DeliveredBy = userName;
        await _c.SaveChangesAsync(ct); return Map(o);
    }

    public async Task<SalesOrderDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status is SalesOrderStatus.Invoiced or SalesOrderStatus.Cancelled) throw new ValidationAppException("الأمر ده اتحوّل لفاتورة أو اتلغى قبل كده.");
        o.Status = SalesOrderStatus.Cancelled; await _c.SaveChangesAsync(ct); return Map(o);
    }

    public async Task<SalesOrderDto> ConvertAsync(Guid id, ConvertSalesOrderDto dto, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status == SalesOrderStatus.Invoiced) throw new ValidationAppException("الأمر ده اتحوّل لفاتورة قبل كده.");
        if (o.Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.Delivered)) throw new ValidationAppException("لازم الأمر يتأكد الأول قبل ما يتحوّل لفاتورة.");
        var invoice = await _sales.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = o.CustomerId, InvoiceDate = dto.InvoiceDate, FiscalPeriodId = dto.FiscalPeriodId, PaymentTerm = dto.PaymentTerm, WarehouseId = dto.WarehouseId,
            Notes = $"من أمر البيع {o.OrderNumber}",
            Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesInvoiceLineInputDto { Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatRate = l.VatRate, ItemId = l.ItemId }).ToList(),
        }, ct);
        o.Status = SalesOrderStatus.Invoiced; o.InvoiceId = invoice.Id;
        await _c.SaveChangesAsync(ct); return Map(o);
    }

    private static SalesOrderDto Map(SalesOrder o) => new()
    {
        Id = o.Id, OrderNumber = o.OrderNumber, OrderDate = o.OrderDate, DeliveryDate = o.DeliveryDate, CustomerId = o.CustomerId, CustomerName = o.Customer?.NameAr ?? string.Empty,
        CustomerPhone = o.Customer?.Mobile ?? o.Customer?.Phone, Status = o.Status, DeliveryAddress = o.DeliveryAddress, Notes = o.Notes, SubTotal = o.SubTotal, VatAmount = o.VatAmount, TotalAmount = o.TotalAmount,
        QuotationId = o.QuotationId, InvoiceId = o.InvoiceId, DeliveredBy = o.DeliveredBy,
        Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new QuotationLineDto { LineNumber = l.LineNumber, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = l.LineTotal, VatRate = l.VatRate, VatAmount = l.VatAmount, ItemId = l.ItemId }).ToList(),
    };
}
