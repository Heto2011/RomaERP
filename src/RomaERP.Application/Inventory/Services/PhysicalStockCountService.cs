using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class PhysicalStockCountService : IPhysicalStockCountService
{
    private readonly IApplicationDbContext _context;
    private readonly IBranchStockService? _branchStock;
    private readonly IStockAdjustmentService? _adjustments;
    private readonly IEmployeeDebtService? _debts;

    public PhysicalStockCountService(IApplicationDbContext context, IBranchStockService? branchStock = null, IStockAdjustmentService? adjustments = null, IEmployeeDebtService? debts = null)
    {
        _debts = debts;
        _context = context;
        _branchStock = branchStock;
        _adjustments = adjustments;
    }

    public async Task<List<PhysicalStockCountDto>> GetAllAsync(CancellationToken ct = default)
    {
        var counts = await _context.PhysicalStockCounts
            .AsNoTracking()
            .Include(c => c.Item).Include(c => c.Warehouse)
            .OrderByDescending(c => c.CountDate)
            .ToListAsync(ct);
        var employeeIds = counts.Where(c => c.ResponsibleEmployeeId != null).Select(c => c.ResponsibleEmployeeId!.Value).Distinct().ToList();
        var names = employeeIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _context.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullNameAr, ct);
        return counts.Select(c =>
        {
            var dto = Map(c);
            if (c.ResponsibleEmployeeId is { } eid && names.TryGetValue(eid, out var n)) dto.ResponsibleEmployeeName = n;
            return dto;
        }).ToList();
    }

    public async Task<PhysicalStockCountDto> CreateAsync(CreatePhysicalStockCountDto dto, CancellationToken ct = default)
    {
        if (dto.CountedQuantity < 0)
            throw new ValidationAppException("الكمية المعدودة لا يمكن أن تكون سالبة.");

        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == dto.ItemId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), dto.ItemId);

        decimal systemQuantity = item.QuantityOnHand;
        Warehouse? warehouse = null;
        if (dto.WarehouseId is { } wid)
        {
            warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wid && !w.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Warehouse), wid);
            if (_branchStock is not null) systemQuantity = await _branchStock.GetBalanceAsync(item.Id, wid, ct);
        }

        var count = new PhysicalStockCount
        {
            ItemId = item.Id,
            WarehouseId = dto.WarehouseId,
            CountDate = dto.CountDate,
            SystemQuantity = systemQuantity,
            CountedQuantity = dto.CountedQuantity,
            UnitCost = item.AverageCost,
            Notes = dto.Notes?.Trim()
        };
        // A difference waits for the owner or accountant; no difference needs nothing.
        count.Status = dto.WarehouseId is null ? 0 : count.CountedQuantity == count.SystemQuantity ? 2 : 1;

        _context.PhysicalStockCounts.Add(count);
        await _context.SaveChangesAsync(ct);

        count.Item = item;
        var result = Map(count);
        result.WarehouseName = warehouse?.NameAr;
        return result;
    }

    /// <summary>The owner or accountant reviews a pending count's difference and decides who bears a shortage; only then is the
    /// stock adjustment (and its journal entry) made.</summary>
    public async Task<PhysicalStockCountDto> ApplyAsync(Guid id, ApproveCountDto decision, string? userName, CancellationToken ct = default)
    {
        if (_adjustments is null) throw new ValidationAppException("التسوية مش متاحة.");
        var count = await _context.PhysicalStockCounts.Include(c => c.Item).Include(c => c.Warehouse).FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PhysicalStockCount), id);
        if (count.Status != 1) throw new ValidationAppException("الجرد ده مش منتظر اعتماد.");
        if (count.WarehouseId is null) throw new ValidationAppException("الجرد القديم مش مربوط بفرع. اعمل جرد جديد لفرع معيّن.");
        var variance = count.CountedQuantity - count.SystemQuantity;
        if (variance == 0) throw new ValidationAppException("مفيش فرق في الجرد.");
        var responsibility = variance < 0 ? (StockLossResponsibility)decision.Responsibility : StockLossResponsibility.Branch;
        if (!Enum.IsDefined(typeof(StockLossResponsibility), responsibility)) throw new ValidationAppException("اختيار المسؤول غير صحيح.");

        var note = string.Join(" - ", new[] { "من جرد فعلي", count.Notes, decision.Note }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var adj = await _adjustments.CreateAsync(new CreateStockAdjustmentDto
        {
            WarehouseId = count.WarehouseId.Value, ItemId = count.ItemId, IsIncrease = variance > 0, Quantity = Math.Abs(variance),
            UnitCost = variance > 0 ? count.UnitCost : null, Reason = variance > 0 ? (int)StockAdjustmentReason.CountGain : (int)StockAdjustmentReason.CountLoss,
            AdjustmentDate = count.CountDate, Notes = note,
            Responsibility = variance < 0 ? (int)responsibility : null, ResponsibleEmployeeId = decision.EmployeeId
        }, userName, ct);

        // A shortage charged to a person is also a debt in their ledger, repaid from pay once the accountant approves each deduction.
        if (variance < 0 && responsibility is StockLossResponsibility.Employee or StockLossResponsibility.Manager && decision.EmployeeId is { } empId && _debts is not null)
        {
            var je = await _context.StockAdjustments.AsNoTracking().Where(a => a.Id == adj.Id).Select(a => a.JournalEntryId).FirstOrDefaultAsync(ct);
            await _debts.AddRecordedDebtAsync(empId, adj.TotalCost, count.CountDate, EmployeeDebtReason.StockShortage, $"نقص مخزون {count.Item!.Code} - {count.Item.NameAr}", adj.Number, je, userName, ct);
        }
        count.StockAdjustmentId = adj.Id;
        count.Status = 2;
        count.Responsibility = variance < 0 ? responsibility : null;
        count.ResponsibleEmployeeId = variance < 0 && responsibility is StockLossResponsibility.Employee or StockLossResponsibility.Manager ? decision.EmployeeId : null;
        count.DecisionNote = decision.Note?.Trim();
        count.DecidedBy = userName;
        count.DecidedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return await MapWithEmployeeAsync(count, ct);
    }

    public async Task<PhysicalStockCountDto> RejectAsync(Guid id, string? note, string? userName, CancellationToken ct = default)
    {
        var count = await _context.PhysicalStockCounts.Include(c => c.Item).Include(c => c.Warehouse).FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PhysicalStockCount), id);
        if (count.Status != 1) throw new ValidationAppException("الجرد ده مش منتظر اعتماد.");
        count.Status = 3;
        count.DecisionNote = note?.Trim();
        count.DecidedBy = userName;
        count.DecidedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Map(count);
    }

    public Task<int> CountPendingAsync(CancellationToken ct = default)
        => _context.PhysicalStockCounts.CountAsync(c => !c.IsDeleted && c.Status == 1, ct);

    private async Task<PhysicalStockCountDto> MapWithEmployeeAsync(PhysicalStockCount c, CancellationToken ct)
    {
        var dto = Map(c);
        if (c.ResponsibleEmployeeId is { } eid)
            dto.ResponsibleEmployeeName = await _context.Employees.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullNameAr).FirstOrDefaultAsync(ct);
        return dto;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var count = await _context.PhysicalStockCounts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(PhysicalStockCount), id);

        count.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<CountVarianceRowDto>> GetVarianceReportAsync(DateTime? from, DateTime? to, Guid? warehouseId, CancellationToken ct = default)
    {
        var q = _context.PhysicalStockCounts.AsNoTracking().Include(c => c.Item).Include(c => c.Warehouse).Where(c => !c.IsDeleted);
        if (from is { } f) q = q.Where(c => c.CountDate >= f.Date);
        if (to is { } t) q = q.Where(c => c.CountDate <= t.Date);
        if (warehouseId is { } w) q = q.Where(c => c.WarehouseId == w);
        var counts = await q.ToListAsync(ct);
        return counts.GroupBy(c => new { c.WarehouseId, c.ItemId }).Select(g =>
        {
            var vars = g.Select(c => (Qty: c.CountedQuantity - c.SystemQuantity, Value: (c.CountedQuantity - c.SystemQuantity) * c.UnitCost)).ToList();
            var first = g.First();
            return new CountVarianceRowDto
            {
                WarehouseId = g.Key.WarehouseId, WarehouseName = first.Warehouse?.NameAr ?? string.Empty, ItemId = g.Key.ItemId,
                ItemCode = first.Item!.Code, ItemName = first.Item.NameAr, Counts = vars.Count,
                ShortQuantity = -vars.Where(v => v.Qty < 0).Sum(v => v.Qty), OverQuantity = vars.Where(v => v.Qty > 0).Sum(v => v.Qty),
                NetQuantity = vars.Sum(v => v.Qty), NetValue = Math.Round(vars.Sum(v => v.Value), 2)
            };
        }).OrderBy(r => r.NetValue).ToList();
    }

    private static PhysicalStockCountDto Map(PhysicalStockCount c)
    {
        var variance = c.CountedQuantity - c.SystemQuantity;
        return new PhysicalStockCountDto
        {
            Id = c.Id,
            ItemId = c.ItemId,
            ItemCode = c.Item!.Code,
            ItemName = c.Item.NameAr,
            CountDate = c.CountDate,
            SystemQuantity = c.SystemQuantity,
            CountedQuantity = c.CountedQuantity,
            Variance = variance,
            UnitCost = c.UnitCost,
            VarianceValue = Math.Round(variance * c.UnitCost, 2),
            Notes = c.Notes,
            WarehouseId = c.WarehouseId,
            WarehouseName = c.Warehouse?.NameAr,
            StockAdjustmentId = c.StockAdjustmentId,
            Status = c.Status,
            Responsibility = c.Responsibility is { } r ? (int)r : null,
            DecisionNote = c.DecisionNote,
            DecidedBy = c.DecidedBy,
            DecidedAt = c.DecidedAt
        };
    }
}
