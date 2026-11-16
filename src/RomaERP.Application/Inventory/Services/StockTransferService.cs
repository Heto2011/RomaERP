using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public interface IStockTransferService
{
    Task<List<StockTransferDto>> GetAllAsync(StockTransferStatus? status, CancellationToken ct = default);
    Task<StockTransferDto> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>Records "Out" at the sending branch: the stock leaves it and travels until the other branch records "In".</summary>
    Task<StockTransferDto> SendAsync(CreateStockTransferDto dto, string? userName, CancellationToken ct = default);
    /// <summary>Records "In" at the receiving branch.</summary>
    Task<StockTransferDto> ReceiveAsync(Guid id, ReceiveStockTransferDto dto, string? userName, CancellationToken ct = default);
    /// <summary>Calls a transfer back before it was received: the stock returns to the sending branch.</summary>
    Task<StockTransferDto> CancelAsync(Guid id, string? userName, CancellationToken ct = default);
    /// <summary>Quantity of each item in each branch (and what is on its way there).</summary>
    Task<List<WarehouseStockRowDto>> GetWarehouseStockAsync(Guid? warehouseId, CancellationToken ct = default);
}

/// <summary>Branch-to-branch stock transfer. The company's stock total and its inventory account never change: a transfer only moves
/// quantities between branches (at the item's average cost), so there is no journal entry and no effect on profit. A branch's quantity
/// is its movement history: receipts and transfers in, minus issues and transfers out.</summary>
public class StockTransferService : IStockTransferService
{
    private readonly IApplicationDbContext _context;
    private readonly IItemLotService _lots;
    private readonly IBranchAccessService? _access;

    public StockTransferService(IApplicationDbContext context, IItemLotService lots, IBranchAccessService? access = null)
    {
        _context = context;
        _lots = lots;
        _access = access;
    }

    private async Task<decimal> BalanceAsync(Guid itemId, Guid warehouseId, CancellationToken ct)
    {
        var movements = await _context.StockMovements.AsNoTracking()
            .Where(m => m.ItemId == itemId && m.WarehouseId == warehouseId)
            .Select(m => new { m.MovementType, m.Quantity }).ToListAsync(ct);
        return movements.Sum(m => m.MovementType is StockMovementType.Receipt or StockMovementType.TransferIn ? m.Quantity : -m.Quantity);
    }

    public async Task<List<StockTransferDto>> GetAllAsync(StockTransferStatus? status, CancellationToken ct = default)
    {
        var query = _context.StockTransfers.AsNoTracking()
            .Include(t => t.FromWarehouse).Include(t => t.ToWarehouse).Include(t => t.Lines).ThenInclude(l => l.Item).AsQueryable();
        if (status is not null) query = query.Where(t => t.Status == status);
        if (_access is not null && await _access.GetRestrictedBranchAsync(ct) is { } branch)
            query = query.Where(t => t.FromWarehouseId == branch || t.ToWarehouseId == branch);
        return (await query.OrderByDescending(t => t.SentAtUtc).Take(300).ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<StockTransferDto> GetAsync(Guid id, CancellationToken ct = default) => Map(await LoadAsync(id, false, ct));

    private async Task<StockTransfer> LoadAsync(Guid id, bool track, CancellationToken ct)
    {
        var query = _context.StockTransfers.Include(t => t.FromWarehouse).Include(t => t.ToWarehouse).Include(t => t.Lines).ThenInclude(l => l.Item).AsQueryable();
        if (!track) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(StockTransfer), id);
    }

    public async Task<StockTransferDto> SendAsync(CreateStockTransferDto dto, string? userName, CancellationToken ct = default)
    {
        if (_access is not null) await _access.EnsureMayUseAsync(dto.FromWarehouseId, ct);
        if (dto.FromWarehouseId == dto.ToWarehouseId)
            throw new ValidationAppException("الفرع المرسل والفرع المستلم لازم يكونوا مختلفين.");
        var warehouses = await _context.Warehouses.Where(w => (w.Id == dto.FromWarehouseId || w.Id == dto.ToWarehouseId) && !w.IsDeleted).ToListAsync(ct);
        var from = warehouses.FirstOrDefault(w => w.Id == dto.FromWarehouseId) ?? throw new NotFoundException(nameof(Warehouse), dto.FromWarehouseId);
        var to = warehouses.FirstOrDefault(w => w.Id == dto.ToWarehouseId) ?? throw new NotFoundException(nameof(Warehouse), dto.ToWarehouseId);
        if (!to.IsActive) throw new ValidationAppException($"الفرع المستلم ({to.NameAr}) مش شغال.");

        if (dto.Lines.Count == 0) throw new ValidationAppException("لازم تضيف صنف واحد على الأقل في التحويل.");
        if (dto.Lines.Any(l => l.Quantity <= 0)) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");

        // The same item (and lot) on two lines is one line.
        var merged = dto.Lines
            .GroupBy(l => (l.ItemId, Lot: (l.LotNumber ?? string.Empty).Trim()))
            .Select(g => new CreateStockTransferLineDto { ItemId = g.Key.ItemId, LotNumber = g.Key.Lot.Length == 0 ? null : g.Key.Lot, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        var number = $"TR-{(await _context.StockTransfers.CountAsync(ct) + 1):D6}";
        var transfer = new StockTransfer
        {
            Number = number, TransferDate = dto.TransferDate == default ? DateTime.UtcNow.Date : dto.TransferDate.Date,
            FromWarehouseId = from.Id, ToWarehouseId = to.Id, Notes = dto.Notes?.Trim(), SentBy = userName, Status = StockTransferStatus.InTransit
        };
        var movementCount = await _context.StockMovements.CountAsync(ct);

        foreach (var line in merged)
        {
            var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == line.ItemId && !i.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Item), line.ItemId);

            var balance = await BalanceAsync(item.Id, from.Id, ct);
            if (line.Quantity > balance)
                throw new ValidationAppException($"رصيد الصنف {item.Code} في {from.NameAr} هو {balance:0.####} والمطلوب تحويله {line.Quantity:0.####}.");

            DateTime? expiry = null;
            if (item.IsLotTracked)
            {
                if (string.IsNullOrWhiteSpace(line.LotNumber))
                    throw new ValidationAppException($"الصنف {item.Code} بيتتبع بالدُفعات: لازم تحدد رقم الدُفعة.");
                var lot = await _context.ItemLots.FirstOrDefaultAsync(l => l.ItemId == item.Id && l.WarehouseId == from.Id && l.LotNumber == line.LotNumber && !l.IsDeleted, ct)
                    ?? throw new ValidationAppException($"الدُفعة {line.LotNumber} مش موجودة في {from.NameAr}.");
                if (lot.QuantityOnHand < line.Quantity)
                    throw new ValidationAppException($"الدُفعة {line.LotNumber} فيها {lot.QuantityOnHand:0.####} بس.");
                lot.QuantityOnHand -= line.Quantity;
                expiry = lot.ExpiryDate;
            }

            var unitCost = item.AverageCost;
            transfer.Lines.Add(new StockTransferLine { ItemId = item.Id, Quantity = line.Quantity, UnitCost = unitCost, LotNumber = line.LotNumber, ExpiryDate = expiry });
            _context.StockMovements.Add(new StockMovement
            {
                MovementNumber = $"SM-{++movementCount:D6}", MovementDate = transfer.TransferDate, MovementType = StockMovementType.TransferOut,
                ItemId = item.Id, WarehouseId = from.Id, Quantity = line.Quantity, UnitCost = unitCost, TotalCost = Math.Round(line.Quantity * unitCost, 2),
                Reference = number, Description = $"تحويل إلى {to.NameAr}"
            });
        }

        _context.StockTransfers.Add(transfer);
        await _context.SaveChangesAsync(ct);
        return await GetAsync(transfer.Id, ct);
    }

    public async Task<StockTransferDto> ReceiveAsync(Guid id, ReceiveStockTransferDto dto, string? userName, CancellationToken ct = default)
    {
        var transfer = await LoadAsync(id, true, ct);
        if (_access is not null) await _access.EnsureMayUseAsync(transfer.ToWarehouseId, ct);
        if (transfer.Status != StockTransferStatus.InTransit)
            throw new ValidationAppException("التحويل ده اتقفل قبل كده (اتستلم أو اتلغى).");

        // What the receiving branch counted, per line (default: everything that was sent).
        var counted = (dto.Lines ?? new()).ToDictionary(l => (l.ItemId, Lot: (l.LotNumber ?? string.Empty).Trim()), l => l.ReceivedQuantity);
        foreach (var line in transfer.Lines)
        {
            var received = counted.TryGetValue((line.ItemId, (line.LotNumber ?? string.Empty).Trim()), out var q) ? q : line.Quantity;
            if (received < 0 || received > line.Quantity)
                throw new ValidationAppException($"الكمية المستلمة من {line.Item?.Code} لازم تكون من صفر لـ {line.Quantity:0.####} (المبعوت).");
            line.ReceivedQuantity = received;
        }

        var shortages = transfer.Lines.Where(l => l.ReceivedQuantity < l.Quantity).ToList();
        Guid? periodId = null;
        if (shortages.Count > 0)
        {
            var today = DateTime.UtcNow.Date;
            var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.StartDate <= today && p.EndDate >= today && !p.IsClosed, ct)
                ?? throw new ValidationAppException("في نقص في الكمية المستلمة، ومفيش فترة محاسبية مفتوحة النهارده تسجّل عليها الهالك.");
            periodId = period.Id;
        }

        var movementCount = await _context.StockMovements.CountAsync(ct);
        var date = DateTime.UtcNow.Date;
        foreach (var line in transfer.Lines)
        {
            // The whole sent quantity arrives in the books, then any shortage is written off in the same step.
            _context.StockMovements.Add(new StockMovement
            {
                MovementNumber = $"SM-{++movementCount:D6}", MovementDate = date, MovementType = StockMovementType.TransferIn,
                ItemId = line.ItemId, WarehouseId = transfer.ToWarehouseId, Quantity = line.Quantity, UnitCost = line.UnitCost,
                TotalCost = Math.Round(line.Quantity * line.UnitCost, 2), Reference = transfer.Number, Description = $"استلام من {transfer.FromWarehouse?.NameAr}"
            });
            await _lots.ReceiveLotAsync(line.ItemId, transfer.ToWarehouseId, line.LotNumber, line.ReceivedQuantity ?? line.Quantity, line.UnitCost, line.ExpiryDate, date, ct);
        }

        foreach (var line in shortages)
        {
            var shortage = line.Quantity - (line.ReceivedQuantity ?? 0);
            var item = await _context.Items.FirstAsync(i => i.Id == line.ItemId, ct);
            var cost = Math.Round(shortage * line.UnitCost, 2);
            var inventory = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.InventoryAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب المخزون ({AccountingConstants.InventoryAccountCode}) غير موجود في دليل الحسابات.");
            var cogs = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.CostOfGoodsSoldAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب تكلفة البضاعة ({AccountingConstants.CostOfGoodsSoldAccountCode}) غير موجود في دليل الحسابات.");

            item.QuantityOnHand -= shortage;
            var entry = await SimpleJournalEntryFactory.CreatePostedAsync(_context, date, periodId!.Value, $"نقص تحويل {transfer.Number} - {item.NameAr}",
                cogs.Id, inventory.Id, cost, "TRANSFER-SHORTAGE", ct, transfer.ToWarehouseId);
            var writeOff = new StockMovement
            {
                MovementNumber = $"SM-{++movementCount:D6}", MovementDate = date, MovementType = StockMovementType.Issue,
                ItemId = line.ItemId, WarehouseId = transfer.ToWarehouseId, Quantity = shortage, UnitCost = line.UnitCost, TotalCost = cost,
                Reference = transfer.Number, Description = $"هالك: نقص عند استلام التحويل {transfer.Number}", JournalEntry = entry
            };
            _context.StockMovements.Add(writeOff);
            _context.WasteEntries.Add(new WasteEntry
            {
                ItemId = line.ItemId, WasteDate = date, Quantity = shortage, UnitCost = line.UnitCost, TotalCost = cost,
                Reason = WasteReason.Unknown, Notes = $"نقص عند استلام التحويل {transfer.Number}", StockMovement = writeOff
            });
        }

        transfer.Status = StockTransferStatus.Received;
        transfer.ReceivedBy = userName;
        transfer.ReceivedAtUtc = DateTime.UtcNow;
        transfer.ReceiveNote = dto.Note?.Trim();
        await _context.SaveChangesAsync(ct);
        return Map(transfer);
    }

    public async Task<StockTransferDto> CancelAsync(Guid id, string? userName, CancellationToken ct = default)
    {
        var transfer = await LoadAsync(id, true, ct);
        if (_access is not null) await _access.EnsureMayUseAsync(transfer.FromWarehouseId, ct);
        if (transfer.Status != StockTransferStatus.InTransit)
            throw new ValidationAppException("التحويل ده اتقفل قبل كده (اتستلم أو اتلغى).");

        await ApplyLegAsync(transfer, transfer.FromWarehouseId, "إلغاء تحويل: رجوع للفرع المرسل", ct);
        transfer.Status = StockTransferStatus.Cancelled;
        transfer.ReceivedBy = userName;
        transfer.ReceivedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Map(transfer);
    }

    /// <summary>Puts the transfer's stock into the given branch: a TransferIn movement per line, and the lot back where it lands.</summary>
    private async Task ApplyLegAsync(StockTransfer transfer, Guid warehouseId, string description, CancellationToken ct)
    {
        var movementCount = await _context.StockMovements.CountAsync(ct);
        var today = DateTime.UtcNow.Date;
        foreach (var line in transfer.Lines)
        {
            _context.StockMovements.Add(new StockMovement
            {
                MovementNumber = $"SM-{++movementCount:D6}", MovementDate = today, MovementType = StockMovementType.TransferIn,
                ItemId = line.ItemId, WarehouseId = warehouseId, Quantity = line.Quantity, UnitCost = line.UnitCost,
                TotalCost = Math.Round(line.Quantity * line.UnitCost, 2), Reference = transfer.Number, Description = description
            });
            await _lots.ReceiveLotAsync(line.ItemId, warehouseId, line.LotNumber, line.Quantity, line.UnitCost, line.ExpiryDate, today, ct);
        }
    }

    public async Task<List<WarehouseStockRowDto>> GetWarehouseStockAsync(Guid? warehouseId, CancellationToken ct = default)
    {
        if (_access is not null && await _access.GetRestrictedBranchAsync(ct) is { } ownBranch) warehouseId = ownBranch;
        var movements = await _context.StockMovements.AsNoTracking()
            .Where(m => warehouseId == null || m.WarehouseId == warehouseId)
            .Select(m => new { m.ItemId, m.WarehouseId, m.MovementType, m.Quantity }).ToListAsync(ct);
        var quantities = movements.GroupBy(m => (m.ItemId, m.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(m => m.MovementType is StockMovementType.Receipt or StockMovementType.TransferIn ? m.Quantity : -m.Quantity));

        var inTransit = await _context.StockTransfers.AsNoTracking().Where(t => t.Status == StockTransferStatus.InTransit)
            .Where(t => warehouseId == null || t.ToWarehouseId == warehouseId)
            .SelectMany(t => t.Lines.Select(l => new { l.ItemId, t.ToWarehouseId, l.Quantity })).ToListAsync(ct);
        var incoming = inTransit.GroupBy(x => (x.ItemId, WarehouseId: x.ToWarehouseId)).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var keys = quantities.Keys.Union(incoming.Keys).ToList();
        var itemIds = keys.Select(k => k.ItemId).Distinct().ToList();
        var warehouseIds = keys.Select(k => k.WarehouseId).Distinct().ToList();
        var items = await _context.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var names = await _context.Warehouses.AsNoTracking().Where(w => warehouseIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.NameAr, ct);

        return keys.Where(k => items.ContainsKey(k.ItemId)).Select(k => new WarehouseStockRowDto
        {
            ItemId = k.ItemId, ItemCode = items[k.ItemId].Code, ItemName = items[k.ItemId].NameAr, UnitOfMeasure = items[k.ItemId].UnitOfMeasure,
            WarehouseId = k.WarehouseId, WarehouseName = names.GetValueOrDefault(k.WarehouseId, "—"),
            Quantity = quantities.GetValueOrDefault(k, 0), IncomingQuantity = incoming.GetValueOrDefault(k, 0)
        }).OrderBy(r => r.ItemCode).ThenBy(r => r.WarehouseName).ToList();
    }

    private static StockTransferDto Map(StockTransfer t) => new()
    {
        Id = t.Id, Number = t.Number, TransferDate = t.TransferDate,
        FromWarehouseId = t.FromWarehouseId, FromWarehouseName = t.FromWarehouse?.NameAr ?? string.Empty,
        ToWarehouseId = t.ToWarehouseId, ToWarehouseName = t.ToWarehouse?.NameAr ?? string.Empty,
        Status = t.Status.ToString(), Notes = t.Notes, SentBy = t.SentBy, SentAtUtc = t.SentAtUtc,
        ReceivedBy = t.ReceivedBy, ReceivedAtUtc = t.ReceivedAtUtc, ReceiveNote = t.ReceiveNote,
        TotalCost = Math.Round(t.Lines.Sum(l => l.Quantity * l.UnitCost), 2),
        Lines = t.Lines.Select(l => new StockTransferLineDto
        {
            ItemId = l.ItemId, ItemCode = l.Item?.Code ?? string.Empty, ItemName = l.Item?.NameAr ?? string.Empty,
            Quantity = l.Quantity, ReceivedQuantity = l.ReceivedQuantity, UnitCost = l.UnitCost, LotNumber = l.LotNumber
        }).ToList()
    };
}
