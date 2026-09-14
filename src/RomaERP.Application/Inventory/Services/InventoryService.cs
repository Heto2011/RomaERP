using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class InventoryService : IInventoryService
{
    private readonly IApplicationDbContext _context;
    private readonly IItemLotService _lotService;

    public InventoryService(IApplicationDbContext context, IItemLotService lotService)
    {
        _context = context;
        _lotService = lotService;
    }

    public async Task<List<StockMovementDto>> GetMovementsAsync(CancellationToken ct = default)
    {
        var movements = await _context.StockMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Warehouse)
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.MovementNumber)
            .ToListAsync(ct);

        return movements.Select(Map).ToList();
    }

    public async Task<StockMovementDto> ReceiveStockAsync(ReceiveStockDto dto, CancellationToken ct = default)
    {
        if (dto.Quantity <= 0)
            throw new ValidationAppException("الكمية يجب أن تكون أكبر من صفر.");
        if (dto.UnitCost < 0)
            throw new ValidationAppException("تكلفة الوحدة لا يمكن أن تكون سالبة.");

        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == dto.ItemId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), dto.ItemId);

        var warehouse = await _context.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct);
        if (!warehouse)
            throw new NotFoundException(nameof(Warehouse), dto.WarehouseId);

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);
        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن تسجيل حركة مخزون لفترة محاسبية مقفلة.");

        var totalCost = dto.Quantity * dto.UnitCost;
        var newQuantity = item.QuantityOnHand + dto.Quantity;
        var newAverageCost = newQuantity == 0
            ? 0
            : ((item.QuantityOnHand * item.AverageCost) + totalCost) / newQuantity;

        item.QuantityOnHand = newQuantity;
        item.AverageCost = Math.Round(newAverageCost, 4);

        await _lotService.ReceiveLotAsync(item.Id, dto.WarehouseId, dto.LotNumber, dto.Quantity, dto.UnitCost, dto.ExpiryDate, dto.MovementDate, ct);

        var (inventoryAccount, _, payableAccount) = await GetInventoryAccountsAsync(ct, needsPayable: true);

        var journalEntry = await BuildJournalEntryAsync(dto.MovementDate, dto.FiscalPeriodId,
            $"استلام مخزون - {item.NameAr}",
            debitAccountId: inventoryAccount.Id, creditAccountId: payableAccount!.Id, amount: totalCost, ct);

        var movement = new StockMovement
        {
            MovementNumber = await GenerateMovementNumberAsync(ct),
            MovementDate = dto.MovementDate,
            MovementType = StockMovementType.Receipt,
            ItemId = item.Id,
            WarehouseId = dto.WarehouseId,
            Quantity = dto.Quantity,
            UnitCost = dto.UnitCost,
            TotalCost = totalCost,
            Reference = dto.Reference,
            Description = dto.Description,
            JournalEntryId = journalEntry.Id
        };

        _context.StockMovements.Add(movement);
        await _context.SaveChangesAsync(ct);

        return await GetMovementDtoAsync(movement.Id, ct);
    }

    public async Task<StockMovementDto> IssueStockAsync(IssueStockDto dto, CancellationToken ct = default)
    {
        if (dto.Quantity <= 0)
            throw new ValidationAppException("الكمية يجب أن تكون أكبر من صفر.");

        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == dto.ItemId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), dto.ItemId);

        var warehouse = await _context.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct);
        if (!warehouse)
            throw new NotFoundException(nameof(Warehouse), dto.WarehouseId);

        if (dto.Quantity > item.QuantityOnHand)
            throw new ValidationAppException($"الكمية المطلوب صرفها ({dto.Quantity}) أكبر من الرصيد المتاح ({item.QuantityOnHand}) للصنف {item.Code}.");

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);
        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن تسجيل حركة مخزون لفترة محاسبية مقفلة.");

        var unitCost = item.AverageCost;
        var totalCost = Math.Round(dto.Quantity * unitCost, 2);

        item.QuantityOnHand -= dto.Quantity;

        await _lotService.ConsumeFefoAsync(item.Id, dto.WarehouseId, dto.Quantity, ct);

        var (inventoryAccount, cogsAccount, _) = await GetInventoryAccountsAsync(ct, needsCogs: true);

        var journalEntry = await BuildJournalEntryAsync(dto.MovementDate, dto.FiscalPeriodId,
            $"صرف مخزون - {item.NameAr}",
            debitAccountId: cogsAccount!.Id, creditAccountId: inventoryAccount.Id, amount: totalCost, ct, costCenterId: dto.CostCenterId);

        var movement = new StockMovement
        {
            MovementNumber = await GenerateMovementNumberAsync(ct),
            MovementDate = dto.MovementDate,
            MovementType = StockMovementType.Issue,
            ItemId = item.Id,
            WarehouseId = dto.WarehouseId,
            CostCenterId = dto.CostCenterId,
            Quantity = dto.Quantity,
            UnitCost = unitCost,
            TotalCost = totalCost,
            Reference = dto.Reference,
            Description = dto.Description,
            JournalEntryId = journalEntry.Id
        };

        _context.StockMovements.Add(movement);
        await _context.SaveChangesAsync(ct);

        return await GetMovementDtoAsync(movement.Id, ct);
    }

    private async Task<(Account Inventory, Account? Cogs, Account? Payable)> GetInventoryAccountsAsync(
        CancellationToken ct, bool needsCogs = false, bool needsPayable = false)
    {
        var inventoryAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.InventoryAccountCode && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب المخزون ({AccountingConstants.InventoryAccountCode}) غير موجود في دليل الحسابات.");

        Account? cogsAccount = null;
        if (needsCogs)
        {
            cogsAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.CostOfGoodsSoldAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب تكلفة البضاعة المباعة ({AccountingConstants.CostOfGoodsSoldAccountCode}) غير موجود في دليل الحسابات.");
        }

        Account? payableAccount = null;
        if (needsPayable)
        {
            payableAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.AccountsPayableAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب الموردين ({AccountingConstants.AccountsPayableAccountCode}) غير موجود في دليل الحسابات.");
        }

        return (inventoryAccount, cogsAccount, payableAccount);
    }

    private async Task<JournalEntry> BuildJournalEntryAsync(DateTime date, Guid fiscalPeriodId, string description,
        Guid debitAccountId, Guid creditAccountId, decimal amount, CancellationToken ct, Guid? costCenterId = null)
    {
        var entryNumber = $"JV-{(await _context.JournalEntries.CountAsync(ct) + 1):D6}";
        var entry = new JournalEntry
        {
            EntryNumber = entryNumber,
            EntryDate = date,
            FiscalPeriodId = fiscalPeriodId,
            Description = description,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = debitAccountId, Debit = amount, Credit = 0, CostCenterId = costCenterId, Description = description },
                new JournalEntryLine { LineNumber = 2, AccountId = creditAccountId, Debit = 0, Credit = amount, CostCenterId = costCenterId, Description = description }
            }
        };

        _context.JournalEntries.Add(entry);
        return entry;
    }

    private async Task<string> GenerateMovementNumberAsync(CancellationToken ct)
    {
        var count = await _context.StockMovements.CountAsync(ct);
        return $"SM-{(count + 1):D6}";
    }

    private async Task<StockMovementDto> GetMovementDtoAsync(Guid id, CancellationToken ct)
    {
        var movement = await _context.StockMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Warehouse)
            .FirstAsync(m => m.Id == id, ct);

        return Map(movement);
    }

    private static StockMovementDto Map(StockMovement m) => new()
    {
        Id = m.Id,
        MovementNumber = m.MovementNumber,
        MovementDate = m.MovementDate,
        MovementType = m.MovementType,
        ItemId = m.ItemId,
        ItemCode = m.Item?.Code ?? string.Empty,
        ItemName = m.Item?.NameAr ?? string.Empty,
        WarehouseId = m.WarehouseId,
        WarehouseName = m.Warehouse?.NameAr ?? string.Empty,
        Quantity = m.Quantity,
        UnitCost = m.UnitCost,
        TotalCost = m.TotalCost,
        Reference = m.Reference,
        Description = m.Description,
        JournalEntryId = m.JournalEntryId
    };
}
