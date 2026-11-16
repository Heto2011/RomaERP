using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public interface IStockAdjustmentService
{
    Task<List<StockAdjustmentDto>> GetAllAsync(CancellationToken ct = default);
    Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentDto dto, string? userName, CancellationToken ct = default);
}

/// <summary>Corrects an item's stock in one branch with a stated reason. Up: the stock is there but was never recorded (no supplier, so no
/// payable): against equity for opening stock, against cost of goods for a count gain. Down: stock that is missing is written off to cost
/// of goods. Quantities, cost, lots and the journal entry are saved together.</summary>
public class StockAdjustmentService : IStockAdjustmentService
{
    private readonly IApplicationDbContext _context;
    private readonly IItemLotService _lots;
    private readonly IBranchAccessService? _access;

    public StockAdjustmentService(IApplicationDbContext context, IItemLotService lots, IBranchAccessService? access = null)
    {
        _context = context;
        _lots = lots;
        _access = access;
    }

    public async Task<List<StockAdjustmentDto>> GetAllAsync(CancellationToken ct = default)
    {
        var query = _context.StockAdjustments.AsNoTracking().Include(a => a.Warehouse).Include(a => a.Item).AsQueryable();
        if (_access is not null && await _access.GetRestrictedBranchAsync(ct) is { } branch) query = query.Where(a => a.WarehouseId == branch);
        return (await query.OrderByDescending(a => a.AdjustmentDate).ThenByDescending(a => a.Number).Take(300).ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentDto dto, string? userName, CancellationToken ct = default)
    {
        if (dto.Quantity <= 0) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");
        if (!Enum.IsDefined(typeof(StockAdjustmentReason), dto.Reason)) throw new ValidationAppException("سبب التسوية غير صحيح.");
        var reason = (StockAdjustmentReason)dto.Reason;
        if (dto.IsIncrease && reason == StockAdjustmentReason.CountLoss) throw new ValidationAppException("«نقص جرد» ينفع مع التسوية بالنقص بس.");
        if (!dto.IsIncrease && reason is StockAdjustmentReason.OpeningStock or StockAdjustmentReason.CountGain)
            throw new ValidationAppException("«رصيد افتتاحي» و«زيادة جرد» ينفعوا مع التسوية بالزيادة بس.");

        if (_access is not null) await _access.EnsureMayUseAsync(dto.WarehouseId, ct);
        var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Warehouse), dto.WarehouseId);
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == dto.ItemId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), dto.ItemId);

        var date = (dto.AdjustmentDate == default ? DateTime.UtcNow : dto.AdjustmentDate).Date;
        var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, ct)
            ?? throw new ValidationAppException($"مفيش فترة محاسبية بتغطي تاريخ {date:yyyy-MM-dd}.");
        if (period.IsClosed) throw new ValidationAppException($"الفترة المحاسبية ({period.Name}) مقفولة.");

        var inventory = await AccountAsync(AccountingConstants.InventoryAccountCode, "المخزون", ct);
        var number = $"ADJ-{(await _context.StockAdjustments.CountAsync(ct) + 1):D6}";
        var movementNumber = $"SM-{(await _context.StockMovements.CountAsync(ct) + 1):D6}";
        var quantity = Math.Round(dto.Quantity, 4);

        decimal unitCost;
        Domain.Accounting.JournalEntry entry;
        if (dto.IsIncrease)
        {
            unitCost = dto.UnitCost ?? item.AverageCost;
            if (unitCost < 0) throw new ValidationAppException("تكلفة الوحدة مش ممكن تكون بالسالب.");
            if (unitCost == 0) throw new ValidationAppException("لازم تحدد تكلفة الوحدة (الصنف ملوش متوسط تكلفة لسه).");
            var total = Math.Round(quantity * unitCost, 2);
            var offsetCode = reason == StockAdjustmentReason.OpeningStock ? AccountingConstants.RetainedEarningsAccountCode : AccountingConstants.CostOfGoodsSoldAccountCode;
            var offset = await AccountAsync(offsetCode, reason == StockAdjustmentReason.OpeningStock ? "الأرباح المرحّلة" : "تكلفة البضاعة المباعة", ct);

            var newQuantity = item.QuantityOnHand + quantity;
            item.AverageCost = newQuantity == 0 ? 0 : Math.Round(((item.QuantityOnHand * item.AverageCost) + (quantity * unitCost)) / newQuantity, 4);
            item.QuantityOnHand = newQuantity;
            await _lots.ReceiveLotAsync(item.Id, warehouse.Id, dto.LotNumber, quantity, unitCost, dto.ExpiryDate, date, ct);

            entry = await SimpleJournalEntryFactory.CreatePostedAsync(_context, date, period.Id, $"تسوية مخزون بالزيادة {number} - {item.NameAr}",
                inventory.Id, offset.Id, total, "STOCK-ADJUSTMENT", ct, warehouse.Id);
        }
        else
        {
            if (quantity > item.QuantityOnHand)
                throw new ValidationAppException($"الكمية ({quantity:0.####}) أكبر من رصيد الصنف {item.Code} في الشركة ({item.QuantityOnHand:0.####}).");
            if (item.IsLotTracked && string.IsNullOrWhiteSpace(dto.LotNumber) is false)
            {
                var lot = await _context.ItemLots.FirstOrDefaultAsync(l => l.ItemId == item.Id && l.WarehouseId == warehouse.Id && l.LotNumber == dto.LotNumber && !l.IsDeleted, ct);
                if (lot is not null) { lot.QuantityOnHand = Math.Max(0, lot.QuantityOnHand - quantity); }
            }
            else
            {
                await _lots.ConsumeFefoAsync(item.Id, warehouse.Id, quantity, ct);
            }
            unitCost = item.AverageCost;
            var total = Math.Round(quantity * unitCost, 2);
            var cogs = await AccountAsync(AccountingConstants.CostOfGoodsSoldAccountCode, "تكلفة البضاعة المباعة", ct);
            item.QuantityOnHand -= quantity;
            entry = await SimpleJournalEntryFactory.CreatePostedAsync(_context, date, period.Id, $"تسوية مخزون بالنقص {number} - {item.NameAr}",
                cogs.Id, inventory.Id, total, "STOCK-ADJUSTMENT", ct, warehouse.Id);
        }

        var totalCost = Math.Round(quantity * unitCost, 2);
        var movement = new StockMovement
        {
            MovementNumber = movementNumber, MovementDate = date,
            MovementType = dto.IsIncrease ? StockMovementType.Receipt : StockMovementType.Issue,
            ItemId = item.Id, WarehouseId = warehouse.Id, Quantity = quantity, UnitCost = unitCost, TotalCost = totalCost,
            Reference = number, Description = $"تسوية مخزون ({reason})" + (string.IsNullOrWhiteSpace(dto.Notes) ? "" : $" - {dto.Notes.Trim()}"),
            JournalEntry = entry
        };
        _context.StockMovements.Add(movement);
        var adjustment = new StockAdjustment
        {
            Number = number, AdjustmentDate = date, WarehouseId = warehouse.Id, ItemId = item.Id, IsIncrease = dto.IsIncrease,
            Quantity = quantity, UnitCost = unitCost, TotalCost = totalCost, Reason = reason, Notes = dto.Notes?.Trim(),
            LotNumber = string.IsNullOrWhiteSpace(dto.LotNumber) ? null : dto.LotNumber.Trim(), CreatedByName = userName, StockMovementId = movement.Id, JournalEntryId = entry.Id
        };
        _context.StockAdjustments.Add(adjustment);
        await _context.SaveChangesAsync(ct);

        var result = Map(adjustment);
        result.WarehouseName = warehouse.NameAr;
        result.ItemCode = item.Code;
        result.ItemName = item.NameAr;
        return result;
    }

    private async Task<Domain.Accounting.Account> AccountAsync(string code, string label, CancellationToken ct)
        => await _context.Accounts.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct)
           ?? throw new ValidationAppException($"حساب {label} ({code}) غير موجود في دليل الحسابات.");

    private static StockAdjustmentDto Map(StockAdjustment a) => new()
    {
        Id = a.Id, Number = a.Number, AdjustmentDate = a.AdjustmentDate, WarehouseId = a.WarehouseId, WarehouseName = a.Warehouse?.NameAr ?? string.Empty,
        ItemId = a.ItemId, ItemCode = a.Item?.Code ?? string.Empty, ItemName = a.Item?.NameAr ?? string.Empty, IsIncrease = a.IsIncrease,
        Quantity = a.Quantity, UnitCost = a.UnitCost, TotalCost = a.TotalCost, Reason = (int)a.Reason, Notes = a.Notes, CreatedByName = a.CreatedByName
    };
}
