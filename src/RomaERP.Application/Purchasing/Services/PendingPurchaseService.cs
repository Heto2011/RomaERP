using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Domain.Purchasing;

namespace RomaERP.Application.Purchasing.Services;

public class PendingPurchaseService : IPendingPurchaseService
{
    private readonly IApplicationDbContext _context;
    private readonly IPurchasingService _purchasing;

    public PendingPurchaseService(IApplicationDbContext context, IPurchasingService purchasing)
    {
        _context = context;
        _purchasing = purchasing;
    }

    public async Task<PendingPurchaseInvoiceDto> SubmitAsync(CreatePurchaseInvoiceDto dto, string userId, string userName, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
            throw new ValidationAppException("لازم يكون في سطر واحد على الأقل في الفاتورة.");
        if (dto.Lines.Any(l => l.Quantity <= 0 || l.UnitPrice < 0))
            throw new ValidationAppException("الكمية لازم تكون أكبر من صفر والسعر مش بالسالب.");

        var vendor = await _context.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == dto.VendorId && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Vendor), dto.VendorId);

        var pending = new PendingPurchaseInvoice
        {
            RequestedByUserId = userId,
            RequestedByName = string.IsNullOrWhiteSpace(userName) ? userId : userName,
            VendorId = vendor.Id,
            VendorName = vendor.NameAr,
            InvoiceDate = dto.InvoiceDate,
            SubTotal = Math.Round(dto.Lines.Sum(l => l.Quantity * l.UnitPrice), 2),
            LineCount = dto.Lines.Count,
            PayloadJson = JsonSerializer.Serialize(dto),
            Status = PendingPurchaseStatus.Pending
        };
        _context.PendingPurchaseInvoices.Add(pending);
        await _context.SaveChangesAsync(ct);
        return Map(pending);
    }

    public async Task<List<PendingPurchaseInvoiceDto>> ListAsync(string userId, bool canApprove, CancellationToken ct = default)
    {
        var query = _context.PendingPurchaseInvoices.AsNoTracking().AsQueryable();
        if (!canApprove)
            query = query.Where(p => p.RequestedByUserId == userId);

        var rows = await query
            .OrderBy(p => p.Status).ThenByDescending(p => p.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<PurchaseInvoiceDto> ApproveAsync(Guid id, string approverName, CancellationToken ct = default)
    {
        var pending = await _context.PendingPurchaseInvoices.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(PendingPurchaseInvoice), id);
        if (pending.Status != PendingPurchaseStatus.Pending)
            throw new ValidationAppException("الفاتورة دي اتحسم أمرها قبل كده.");

        var dto = JsonSerializer.Deserialize<CreatePurchaseInvoiceDto>(pending.PayloadJson)
            ?? throw new ValidationAppException("بيانات الفاتورة تالفة، ارفضها واطلب إدخالها من جديد.");

        // Posts the real invoice exactly as an Admin or Accountant entering it would (journal entry, vendor balance).
        var invoice = await _purchasing.CreateInvoiceAsync(dto, ct);

        if (dto.ReceiveToWarehouseId is { } warehouseId)
        {
            var stockLines = dto.Lines.Where(l => l.ItemId.HasValue && l.Quantity > 0).ToList();
            if (stockLines.Count > 0)
            {
                await _purchasing.ReceiveInventoryPurchaseAsync(new ReceiveInventoryPurchaseDto
                {
                    VendorId = dto.VendorId,
                    ReceiptDate = dto.InvoiceDate,
                    WarehouseId = warehouseId,
                    Notes = dto.Notes,
                    Lines = stockLines.Select(l => new ReceiveInventoryPurchaseLineInputDto
                    {
                        ItemId = l.ItemId!.Value,
                        Quantity = l.Quantity,
                        UnitCost = l.UnitPrice
                    }).ToList()
                }, ct);
            }
        }

        pending.Status = PendingPurchaseStatus.Approved;
        pending.DecidedByName = approverName;
        pending.DecidedAtUtc = DateTime.UtcNow;
        pending.ApprovedInvoiceId = invoice.Id;
        await _context.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task RejectAsync(Guid id, string approverName, string? reason, CancellationToken ct = default)
    {
        var pending = await _context.PendingPurchaseInvoices.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(PendingPurchaseInvoice), id);
        if (pending.Status != PendingPurchaseStatus.Pending)
            throw new ValidationAppException("الفاتورة دي اتحسم أمرها قبل كده.");

        pending.Status = PendingPurchaseStatus.Rejected;
        pending.DecidedByName = approverName;
        pending.DecidedAtUtc = DateTime.UtcNow;
        pending.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await _context.SaveChangesAsync(ct);
    }

    private static PendingPurchaseInvoiceDto Map(PendingPurchaseInvoice p) => new()
    {
        Id = p.Id,
        RequestedByName = p.RequestedByName,
        RequestedAtUtc = p.CreatedAtUtc,
        VendorName = p.VendorName,
        InvoiceDate = p.InvoiceDate,
        SubTotal = p.SubTotal,
        LineCount = p.LineCount,
        Status = p.Status.ToString(),
        DecidedByName = p.DecidedByName,
        RejectionReason = p.RejectionReason
    };
}
