using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Domain.Purchasing;

namespace RomaERP.Application.Purchasing.Services;

public interface IPurchaseOrderService
{
    Task<List<PurchaseOrderDto>> GetAllAsync(CancellationToken ct = default);
    Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderDto dto, CancellationToken ct = default);
    Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderDto dto, CancellationToken ct = default);
    Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderDto> CancelAsync(Guid id, CancellationToken ct = default);
    /// <summary>Books the supplier's invoice from an approved order through the normal purchasing service, so every invoice rule still applies.</summary>
    Task<PurchaseOrderDto> ConvertAsync(Guid id, ConvertPurchaseOrderDto dto, CancellationToken ct = default);
}

/// <summary>Orders placed with suppliers. They carry no accounting: the books only see them when the supplier's invoice is booked from an approved order.</summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IApplicationDbContext _c;
    private readonly IPurchasingService _purchasing;
    private readonly ICurrentUserService _user;
    public PurchaseOrderService(IApplicationDbContext c, IPurchasingService purchasing, ICurrentUserService user) { _c = c; _purchasing = purchasing; _user = user; }

    public async Task<List<PurchaseOrderDto>> GetAllAsync(CancellationToken ct = default)
        => (await _c.PurchaseOrders.AsNoTracking().Include(o => o.Vendor).Include(o => o.Lines).Where(o => !o.IsDeleted).OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderNumber).Take(500).ToListAsync(ct)).Select(Map).ToList();

    public async Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken ct = default) => Map(await LoadAsync(id, false, ct));

    private async Task<PurchaseOrder> LoadAsync(Guid id, bool track, CancellationToken ct)
    {
        var q = _c.PurchaseOrders.Include(x => x.Vendor).Include(x => x.Lines).Where(x => x.Id == id && !x.IsDeleted);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(PurchaseOrder), id);
    }

    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderDto dto, CancellationToken ct = default)
    {
        var o = new PurchaseOrder { OrderNumber = $"PO-{(await _c.PurchaseOrders.CountAsync(ct) + 1):D6}" };
        foreach (var l in await FillAsync(o, dto, ct)) o.Lines.Add(l);
        _c.PurchaseOrders.Add(o);
        await _c.SaveChangesAsync(ct);
        return await GetAsync(o.Id, ct);
    }

    public async Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderDto dto, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status != PurchaseOrderStatus.Draft) throw new ValidationAppException("الأمر ده اتعتمد أو اتقفل ومينفعش يتعدّل.");
        var newLines = await FillAsync(o, dto, ct);
        _c.PurchaseOrderLines.RemoveRange(o.Lines.ToList());
        // New lines are added through their own set: a line added to a tracked order's collection with a preset key would be treated as an existing row.
        foreach (var l in newLines) { l.PurchaseOrderId = o.Id; _c.PurchaseOrderLines.Add(l); }
        await _c.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<List<PurchaseOrderLine>> FillAsync(PurchaseOrder o, SavePurchaseOrderDto dto, CancellationToken ct)
    {
        if (dto.Lines.Count == 0) throw new ValidationAppException("لازم يكون في بند واحد على الأقل.");
        if (dto.Lines.Any(l => string.IsNullOrWhiteSpace(l.Description))) throw new ValidationAppException("اكتب وصف لكل بند.");
        if (dto.Lines.Any(l => l.Quantity <= 0)) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");
        if (dto.Lines.Any(l => l.UnitPrice < 0)) throw new ValidationAppException("السعر مينفعش يكون بالسالب.");
        if (dto.Lines.Any(l => l.VatRate is < 0 or > 1)) throw new ValidationAppException("نسبة الضريبة لازم تكون بين 0 و100%.");
        if (dto.ExpectedDate is { } e && e.Date < dto.OrderDate.Date) throw new ValidationAppException("تاريخ التوريد المتوقع قبل تاريخ الأمر.");
        if (!await _c.Vendors.AnyAsync(v => v.Id == dto.VendorId && !v.IsDeleted, ct)) throw new NotFoundException(nameof(Vendor), dto.VendorId);
        var vat = (await _c.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct))?.VatRate ?? 0;
        o.VendorId = dto.VendorId; o.OrderDate = dto.OrderDate.Date; o.ExpectedDate = dto.ExpectedDate?.Date;
        var notes = dto.Notes?.Trim(); o.Notes = string.IsNullOrEmpty(notes) ? null : notes.Length > 1000 ? notes[..1000] : notes;
        var n = 0;
        var built = new List<PurchaseOrderLine>();
        foreach (var l in dto.Lines)
        {
            var total = Math.Round(l.Quantity * l.UnitPrice, 2); var rate = l.VatRate ?? vat;
            built.Add(new PurchaseOrderLine { LineNumber = ++n, Description = l.Description.Trim(), Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = total, VatRate = rate, VatAmount = Math.Round(total * rate, 2), ItemId = l.ItemId, AccountId = l.AccountId });
        }
        o.SubTotal = built.Sum(l => l.LineTotal);
        o.VatAmount = built.GroupBy(l => l.VatRate).Sum(g => Math.Round(g.Sum(l => l.LineTotal) * g.Key, 2));
        o.TotalAmount = o.SubTotal + o.VatAmount;
        return built;
    }

    public async Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status != PurchaseOrderStatus.Draft) throw new ValidationAppException("الأمر ده مش مسودة.");
        o.Status = PurchaseOrderStatus.Approved; o.ApprovedBy = _user.UserName; o.ApprovedAtUtc = DateTime.UtcNow;
        await _c.SaveChangesAsync(ct);
        return Map(o);
    }

    public async Task<PurchaseOrderDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status is PurchaseOrderStatus.Converted or PurchaseOrderStatus.Cancelled) throw new ValidationAppException("الأمر ده اتحوّل لفاتورة أو اتلغى قبل كده.");
        o.Status = PurchaseOrderStatus.Cancelled;
        await _c.SaveChangesAsync(ct);
        return Map(o);
    }

    public async Task<PurchaseOrderDto> ConvertAsync(Guid id, ConvertPurchaseOrderDto dto, CancellationToken ct = default)
    {
        var o = await LoadAsync(id, true, ct);
        if (o.Status == PurchaseOrderStatus.Converted) throw new ValidationAppException("الأمر ده اتحوّل لفاتورة قبل كده.");
        if (o.Status != PurchaseOrderStatus.Approved) throw new ValidationAppException("لازم الأمر يتعتمد الأول قبل ما يتحوّل لفاتورة.");
        if (o.Lines.Any(l => l.AccountId is null) && dto.DefaultAccountId is null) throw new ValidationAppException("في بنود من غير حساب. اختار حساب للفاتورة.");
        var invoice = await _purchasing.CreateInvoiceAsync(new CreatePurchaseInvoiceDto
        {
            VendorId = o.VendorId, InvoiceDate = dto.InvoiceDate, FiscalPeriodId = dto.FiscalPeriodId, PaymentTerm = dto.PaymentTerm, ReceiveToWarehouseId = dto.ReceiveToWarehouseId,
            Notes = $"من أمر الشراء {o.OrderNumber}",
            Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new PurchaseInvoiceLineInputDto { Description = l.Description, AccountId = l.AccountId ?? dto.DefaultAccountId!.Value, ItemId = l.ItemId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatRate = l.VatRate }).ToList(),
        }, ct);
        o.Status = PurchaseOrderStatus.Converted;
        o.ConvertedInvoiceId = invoice.Id;
        await _c.SaveChangesAsync(ct);
        return Map(o);
    }

    private static PurchaseOrderDto Map(PurchaseOrder o) => new()
    {
        Id = o.Id, OrderNumber = o.OrderNumber, OrderDate = o.OrderDate, ExpectedDate = o.ExpectedDate, VendorId = o.VendorId, VendorName = o.Vendor?.NameAr ?? string.Empty,
        Status = o.Status, Notes = o.Notes, SubTotal = o.SubTotal, VatAmount = o.VatAmount, TotalAmount = o.TotalAmount, ApprovedBy = o.ApprovedBy, ApprovedAtUtc = o.ApprovedAtUtc, ConvertedInvoiceId = o.ConvertedInvoiceId,
        Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new PurchaseOrderLineDto { LineNumber = l.LineNumber, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = l.LineTotal, VatRate = l.VatRate, VatAmount = l.VatAmount, ItemId = l.ItemId, AccountId = l.AccountId }).ToList(),
    };
}
