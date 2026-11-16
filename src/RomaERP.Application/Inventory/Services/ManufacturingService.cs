using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class ManufacturingService : IManufacturingService
{
    private readonly IApplicationDbContext _context;
    private readonly IItemLotService _lotService;

    private readonly IBranchStockService? _branchStock;
    private readonly IBranchAccessService? _branchAccess;

    public ManufacturingService(IApplicationDbContext context, IItemLotService lotService, IBranchStockService? branchStock = null, IBranchAccessService? branchAccess = null)
    {
        _branchStock = branchStock;
        _branchAccess = branchAccess;
        _context = context;
        _lotService = lotService;
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
        if (dto.ConversionCostPerBatch < 0) throw new ValidationAppException("تكلفة التشغيل للباتش مش ممكن تكون بالسالب.");
        bom.OutputQuantity = dto.OutputQuantity;
        bom.ConversionCostPerBatch = Math.Round(dto.ConversionCostPerBatch, 2);
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
        if (_branchAccess is not null) await _branchAccess.EnsureMayUseAsync(dto.WarehouseId, ct);

        foreach (var line in bom.Lines)
        {
            var consumedQty = line.QuantityPerBatch * scale;
            if (consumedQty > line.RawMaterialItem!.QuantityOnHand)
            {
                throw new ValidationAppException(
                    $"الكمية المطلوبة ({consumedQty:0.####}) من {line.RawMaterialItem.NameAr} أكبر من الرصيد المتاح ({line.RawMaterialItem.QuantityOnHand:0.####}).");
            }
            if (_branchStock is not null) await _branchStock.EnsureAvailableAsync(line.RawMaterialItem, dto.WarehouseId, consumedQty, ct);
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

            await _lotService.ConsumeFefoAsync(rawMaterial.Id, dto.WarehouseId, consumedQty, ct);

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

        order.MaterialCost = totalCost;
        order.Batches = Math.Round(scale, 4);

        // IAS 2: the cost of produced stock also carries conversion costs. Variable ones (labour, energy) at the recipe's per-batch rate, fixed
        // production overhead at the site's rate on NORMAL capacity, capped so a month never absorbs more than the overhead the site really has.
        var variableConversion = Math.Round(bom.ConversionCostPerBatch * scale, 2);
        var fixedAllocated = 0m;
        var site = await _context.ManufacturingSites.AsNoTracking().FirstOrDefaultAsync(s => s.WarehouseId == dto.WarehouseId && s.IsActive && !s.IsDeleted, ct);
        if (site is not null && site.NormalMonthlyBatches > 0 && site.MonthlyFixedOverhead > 0)
        {
            var monthStart = new DateTime(dto.ProductionDate.Year, dto.ProductionDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var absorbedSoFar = await _context.ManufacturingOrders.Where(o => o.WarehouseId == dto.WarehouseId && o.ProductionDate >= monthStart && o.ProductionDate < monthEnd && !o.IsDeleted)
                .SumAsync(o => (decimal?)o.FixedOverheadAllocated, ct) ?? 0m;
            var rate = site.MonthlyFixedOverhead / site.NormalMonthlyBatches;
            fixedAllocated = Math.Round(Math.Max(0m, Math.Min(rate * scale, site.MonthlyFixedOverhead - absorbedSoFar)), 2);
        }
        order.ConversionCost = variableConversion;
        order.FixedOverheadAllocated = fixedAllocated;
        var conversionTotal = variableConversion + fixedAllocated;
        totalCost += conversionTotal;
        order.TotalCost = totalCost;

        if (conversionTotal > 0)
        {
            var date = dto.ProductionDate.Date;
            var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, ct)
                ?? throw new ValidationAppException($"مفيش فترة محاسبية بتغطي تاريخ {date:yyyy-MM-dd}، ومطلوبة لتحميل تكاليف التصنيع على المخزون.");
            if (period.IsClosed) throw new ValidationAppException($"الفترة المحاسبية ({period.Name}) مقفولة.");
            var inventoryAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.InventoryAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException("حساب المخزون غير موجود في دليل الحسابات.");
            var capitalised = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.CapitalisedConversionCostAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب تكاليف التصنيع المحمّلة على المخزون ({AccountingConstants.CapitalisedConversionCostAccountCode}) غير موجود في دليل الحسابات.");
            var entry = await SimpleJournalEntryFactory.CreatePostedAsync(_context, date, period.Id, $"تحميل تكاليف تصنيع على المخزون - {orderNumber}",
                inventoryAccount.Id, capitalised.Id, conversionTotal, orderNumber, ct, dto.WarehouseId);
            order.ConversionJournalEntryId = entry.Id;
        }

        var outputItem = bom.OutputItem!;
        var newQuantity = outputItem.QuantityOnHand + dto.ProducedQuantity;
        var newAverageCost = newQuantity == 0 ? 0 : ((outputItem.QuantityOnHand * outputItem.AverageCost) + totalCost) / newQuantity;
        outputItem.QuantityOnHand = newQuantity;
        outputItem.AverageCost = Math.Round(newAverageCost, 4);

        var outputUnitCost = dto.ProducedQuantity == 0 ? 0 : Math.Round(totalCost / dto.ProducedQuantity, 4);
        await _lotService.ReceiveLotAsync(outputItem.Id, dto.WarehouseId, dto.OutputLotNumber, dto.ProducedQuantity, outputUnitCost, dto.OutputExpiryDate, dto.ProductionDate, ct);

        stockMovements.Add(new StockMovement
        {
            MovementNumber = $"SM-{(movementCount + ++movementIndex):D6}",
            MovementDate = dto.ProductionDate,
            MovementType = StockMovementType.Receipt,
            ItemId = outputItem.Id,
            WarehouseId = dto.WarehouseId,
            Quantity = dto.ProducedQuantity,
            UnitCost = outputUnitCost,
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

    public async Task<List<ManufacturingSiteDto>> GetSitesAsync(CancellationToken ct = default)
    {
        var sites = await _context.ManufacturingSites.AsNoTracking().Include(s => s.Warehouse).Where(s => !s.IsDeleted).ToListAsync(ct);
        return sites.Select(MapSite).ToList();
    }

    public async Task<ManufacturingSiteDto> SetSiteAsync(Guid warehouseId, SetManufacturingSiteDto dto, CancellationToken ct = default)
    {
        if (dto.MonthlyFixedOverhead < 0) throw new ValidationAppException("المصروفات الثابتة مش ممكن تكون بالسالب.");
        if (dto.MonthlyFixedOverhead > 0 && dto.NormalMonthlyBatches <= 0)
            throw new ValidationAppException("لازم تحدد الطاقة العادية (عدد الباتشات في الشهر العادي) علشان تتوزع المصروفات الثابتة عليها.");
        var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Warehouse), warehouseId);
        var site = await _context.ManufacturingSites.FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && !s.IsDeleted, ct);
        if (site is null) { site = new ManufacturingSite { WarehouseId = warehouseId }; _context.ManufacturingSites.Add(site); }
        site.MonthlyFixedOverhead = Math.Round(dto.MonthlyFixedOverhead, 2);
        site.NormalMonthlyBatches = Math.Round(dto.NormalMonthlyBatches, 4);
        site.IsActive = dto.IsActive;
        await _context.SaveChangesAsync(ct);
        var result = MapSite(site);
        result.WarehouseName = warehouse.NameAr;
        return result;
    }

    public async Task<List<OverheadAbsorptionRowDto>> GetOverheadReportAsync(int year, int month, CancellationToken ct = default)
    {
        if (month < 1 || month > 12 || year < 2000) throw new ValidationAppException("الشهر أو السنة غير صحيحة.");
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var sites = await _context.ManufacturingSites.AsNoTracking().Include(s => s.Warehouse).Where(s => !s.IsDeleted && s.IsActive).ToListAsync(ct);
        var orders = await _context.ManufacturingOrders.AsNoTracking().Where(o => !o.IsDeleted && o.ProductionDate >= start && o.ProductionDate < end).ToListAsync(ct);
        return sites.Select(s =>
        {
            var mine = orders.Where(o => o.WarehouseId == s.WarehouseId).ToList();
            var batches = mine.Sum(o => o.Batches);
            var absorbed = mine.Sum(o => o.FixedOverheadAllocated);
            return new OverheadAbsorptionRowDto
            {
                WarehouseId = s.WarehouseId, WarehouseName = s.Warehouse?.NameAr ?? string.Empty, MonthlyFixedOverhead = s.MonthlyFixedOverhead,
                NormalMonthlyBatches = s.NormalMonthlyBatches, ActualBatches = batches,
                CapacityUsedPercent = s.NormalMonthlyBatches == 0 ? 0 : Math.Round(batches / s.NormalMonthlyBatches * 100, 1),
                Absorbed = absorbed, Unabsorbed = Math.Max(0, s.MonthlyFixedOverhead - absorbed)
            };
        }).ToList();
    }

    private static ManufacturingSiteDto MapSite(ManufacturingSite s) => new()
    {
        WarehouseId = s.WarehouseId, WarehouseName = s.Warehouse?.NameAr ?? string.Empty, MonthlyFixedOverhead = s.MonthlyFixedOverhead,
        NormalMonthlyBatches = s.NormalMonthlyBatches, IsActive = s.IsActive,
        RatePerBatch = s.NormalMonthlyBatches == 0 ? 0 : Math.Round(s.MonthlyFixedOverhead / s.NormalMonthlyBatches, 2)
    };

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
        ConversionCostPerBatch = b.ConversionCostPerBatch,
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
        MaterialCost = o.MaterialCost,
        ConversionCost = o.ConversionCost,
        FixedOverheadAllocated = o.FixedOverheadAllocated,
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
