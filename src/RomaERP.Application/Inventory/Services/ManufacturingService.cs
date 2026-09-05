using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class ManufacturingService : IManufacturingService
{
    private readonly IApplicationDbContext _context;

    public ManufacturingService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ManufacturingBomDto>> GetBomsAsync(CancellationToken ct = default)
    {
        var boms = await _context.ManufacturingBoms
            .AsNoTracking()
            .Include(b => b.OutputItem)
            .Include(b => b.Lines).ThenInclude(l => l.RawMaterialItem)
            .Where(b => !b.IsDeleted)
            .ToListAsync(ct);

        return boms.Select(Map).ToList();
    }

    public async Task<ManufacturingBomDto?> GetBomByOutputItemAsync(Guid outputItemId, CancellationToken ct = default)
    {
        var bom = await _context.ManufacturingBoms
            .AsNoTracking()
            .Include(b => b.OutputItem)
            .Include(b => b.Lines).ThenInclude(l => l.RawMaterialItem)
            .FirstOrDefaultAsync(b => b.OutputItemId == outputItemId && !b.IsDeleted, ct);

        return bom is null ? null : Map(bom);
    }

    public async Task<ManufacturingBomDto> SetBomAsync(Guid outputItemId, SetManufacturingBomDto dto, CancellationToken ct = default)
    {
        var outputItem = await _context.Items.FirstOrDefaultAsync(i => i.Id == outputItemId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), outputItemId);

        if (dto.OutputQuantity <= 0)
            throw new ValidationAppException("كمية الإنتاج للباتش يجب أن تكون أكبر من صفر.");
        if (dto.Lines.Count == 0)
            throw new ValidationAppException("لازم يكون في مكوّن خام واحد على الأقل.");

        var rawMaterialIds = dto.Lines.Select(l => l.RawMaterialItemId).ToList();
        if (rawMaterialIds.Distinct().Count() != rawMaterialIds.Count)
            throw new ValidationAppException("مفيش داعي لتكرار نفس المكوّن أكتر من مرة في نفس التصنيع.");
        if (rawMaterialIds.Contains(outputItemId))
            throw new ValidationAppException("الصنف مينفعش يكون مكوّن في تصنيع نفسه.");

        foreach (var line in dto.Lines)
        {
            if (line.QuantityPerBatch <= 0)
                throw new ValidationAppException("كمية المكوّن لكل باتش يجب أن تكون أكبر من صفر.");
        }

        var existingCount = await _context.Items.CountAsync(i => rawMaterialIds.Contains(i.Id) && !i.IsDeleted, ct);
        if (existingCount != rawMaterialIds.Distinct().Count())
            throw new ValidationAppException("في مكوّن (صنف مخزون) غير موجود في القائمة.");

        var bom = await _context.ManufacturingBoms.FirstOrDefaultAsync(b => b.OutputItemId == outputItemId && !b.IsDeleted, ct);
        if (bom is null)
        {
            bom = new ManufacturingBom { OutputItemId = outputItemId };
            _context.ManufacturingBoms.Add(bom);
        }
        bom.OutputQuantity = dto.OutputQuantity;
        bom.IsActive = true;

        var existingLines = await _context.ManufacturingBomLines.Where(l => l.BomId == bom.Id).ToListAsync(ct);
        _context.ManufacturingBomLines.RemoveRange(existingLines);

        foreach (var line in dto.Lines)
        {
            _context.ManufacturingBomLines.Add(new ManufacturingBomLine
            {
                BomId = bom.Id,
                RawMaterialItemId = line.RawMaterialItemId,
                QuantityPerBatch = line.QuantityPerBatch
            });
        }

        await _context.SaveChangesAsync(ct);

        return await GetBomByOutputItemAsync(outputItemId, ct) ?? throw new InvalidOperationException("BOM was just saved but could not be re-read.");
    }

    public async Task DeleteBomAsync(Guid outputItemId, CancellationToken ct = default)
    {
        var bom = await _context.ManufacturingBoms.FirstOrDefaultAsync(b => b.OutputItemId == outputItemId && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(ManufacturingBom), outputItemId);

        bom.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ManufacturingOrderDto>> GetOrdersAsync(CancellationToken ct = default)
    {
        var orders = await _context.ManufacturingOrders
            .AsNoTracking()
            .Include(o => o.Bom).ThenInclude(b => b!.OutputItem)
            .Include(o => o.Warehouse)
            .Include(o => o.Lines).ThenInclude(l => l.RawMaterialItem)
            .OrderByDescending(o => o.ProductionDate)
            .ThenByDescending(o => o.OrderNumber)
            .ToListAsync(ct);

        return orders.Select(Map).ToList();
    }

    public async Task<ManufacturingOrderDto> CreateOrderAsync(CreateManufacturingOrderDto dto, CancellationToken ct = default)
    {
        if (dto.ProducedQuantity <= 0)
            throw new ValidationAppException("الكمية المنتجة يجب أن تكون أكبر من صفر.");

        var bom = await _context.ManufacturingBoms
            .Include(b => b.OutputItem)
            .Include(b => b.Lines).ThenInclude(l => l.RawMaterialItem)
            .FirstOrDefaultAsync(b => b.OutputItemId == dto.OutputItemId && !b.IsDeleted, ct)
            ?? throw new ValidationAppException("الصنف ده مفيهوش وصفة تصنيع معرّفة. عرّف وصفة الإنتاج الأول.");

        var warehouse = await _context.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct);
        if (!warehouse)
            throw new NotFoundException(nameof(Warehouse), dto.WarehouseId);

        var scale = dto.ProducedQuantity / bom.OutputQuantity;

        foreach (var line in bom.Lines)
        {
            var consumedQty = line.QuantityPerBatch * scale;
            if (consumedQty > line.RawMaterialItem!.QuantityOnHand)
            {
                throw new ValidationAppException(
                    $"الكمية المطلوبة ({consumedQty:0.####}) من {line.RawMaterialItem.NameAr} أكبر من الرصيد المتاح ({line.RawMaterialItem.QuantityOnHand:0.####}).");
            }
        }

        var orderNumber = $"MFG-{(await _context.ManufacturingOrders.CountAsync(ct) + 1):D6}";
        var order = new ManufacturingOrder
        {
            OrderNumber = orderNumber,
            BomId = bom.Id,
            WarehouseId = dto.WarehouseId,
            ProductionDate = dto.ProductionDate,
            ProducedQuantity = dto.ProducedQuantity,
            Notes = dto.Notes?.Trim(),
        };

        decimal totalCost = 0;
        var stockMovements = new List<StockMovement>();
        var movementCount = await _context.StockMovements.CountAsync(ct);
        var movementIndex = 0;

        foreach (var line in bom.Lines)
        {
            var rawMaterial = line.RawMaterialItem!;
            var consumedQty = Math.Round(line.QuantityPerBatch * scale, 4);
            var unitCost = rawMaterial.AverageCost;
            var lineCost = Math.Round(consumedQty * unitCost, 2);

            rawMaterial.QuantityOnHand -= consumedQty;
            totalCost += lineCost;

            order.Lines.Add(new ManufacturingOrderLine
            {
                RawMaterialItemId = rawMaterial.Id,
                QuantityConsumed = consumedQty,
                UnitCost = unitCost,
                TotalCost = lineCost,
            });

            stockMovements.Add(new StockMovement
            {
                MovementNumber = $"SM-{(movementCount + ++movementIndex):D6}",
                MovementDate = dto.ProductionDate,
                MovementType = StockMovementType.Issue,
                ItemId = rawMaterial.Id,
                WarehouseId = dto.WarehouseId,
                Quantity = consumedQty,
                UnitCost = unitCost,
                TotalCost = lineCost,
                Reference = orderNumber,
                Description = $"استهلاك تصنيع - {orderNumber}",
                JournalEntryId = null
            });
        }

        order.TotalCost = totalCost;

        var outputItem = bom.OutputItem!;
        var newQuantity = outputItem.QuantityOnHand + dto.ProducedQuantity;
        var newAverageCost = newQuantity == 0 ? 0 : ((outputItem.QuantityOnHand * outputItem.AverageCost) + totalCost) / newQuantity;
        outputItem.QuantityOnHand = newQuantity;
        outputItem.AverageCost = Math.Round(newAverageCost, 4);

        stockMovements.Add(new StockMovement
        {
            MovementNumber = $"SM-{(movementCount + ++movementIndex):D6}",
            MovementDate = dto.ProductionDate,
            MovementType = StockMovementType.Receipt,
            ItemId = outputItem.Id,
            WarehouseId = dto.WarehouseId,
            Quantity = dto.ProducedQuantity,
            UnitCost = dto.ProducedQuantity == 0 ? 0 : Math.Round(totalCost / dto.ProducedQuantity, 4),
            TotalCost = totalCost,
            Reference = orderNumber,
            Description = $"إنتاج تصنيع - {orderNumber}",
            JournalEntryId = null
        });

        _context.ManufacturingOrders.Add(order);
        _context.StockMovements.AddRange(stockMovements);
        await _context.SaveChangesAsync(ct);

        return await GetOrderDtoAsync(order.Id, ct);
    }

    private async Task<ManufacturingOrderDto> GetOrderDtoAsync(Guid id, CancellationToken ct)
    {
        var order = await _context.ManufacturingOrders
            .AsNoTracking()
            .Include(o => o.Bom).ThenInclude(b => b!.OutputItem)
            .Include(o => o.Warehouse)
            .Include(o => o.Lines).ThenInclude(l => l.RawMaterialItem)
            .FirstAsync(o => o.Id == id, ct);

        return Map(order);
    }

    private static ManufacturingBomDto Map(ManufacturingBom b) => new()
    {
        Id = b.Id,
        OutputItemId = b.OutputItemId,
        OutputItemCode = b.OutputItem?.Code ?? string.Empty,
        OutputItemName = b.OutputItem?.NameAr ?? string.Empty,
        OutputQuantity = b.OutputQuantity,
        Lines = b.Lines.Select(l => new ManufacturingBomLineDto
        {
            RawMaterialItemId = l.RawMaterialItemId,
            RawMaterialItemCode = l.RawMaterialItem?.Code ?? string.Empty,
            RawMaterialItemName = l.RawMaterialItem?.NameAr ?? string.Empty,
            QuantityPerBatch = l.QuantityPerBatch
        }).ToList()
    };

    private static ManufacturingOrderDto Map(ManufacturingOrder o) => new()
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        OutputItemId = o.Bom?.OutputItemId ?? Guid.Empty,
        OutputItemCode = o.Bom?.OutputItem?.Code ?? string.Empty,
        OutputItemName = o.Bom?.OutputItem?.NameAr ?? string.Empty,
        WarehouseId = o.WarehouseId,
        WarehouseName = o.Warehouse?.NameAr ?? string.Empty,
        ProductionDate = o.ProductionDate,
        ProducedQuantity = o.ProducedQuantity,
        TotalCost = o.TotalCost,
        Notes = o.Notes,
        Lines = o.Lines.Select(l => new ManufacturingOrderLineDto
        {
            RawMaterialItemId = l.RawMaterialItemId,
            RawMaterialItemCode = l.RawMaterialItem?.Code ?? string.Empty,
            RawMaterialItemName = l.RawMaterialItem?.NameAr ?? string.Empty,
            QuantityConsumed = l.QuantityConsumed,
            UnitCost = l.UnitCost,
            TotalCost = l.TotalCost
        }).ToList()
    };
}
