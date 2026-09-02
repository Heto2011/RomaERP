using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Restaurant;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Restaurant.Services;

public class RestaurantService : IRestaurantService
{
    private readonly IApplicationDbContext _context;
    private readonly ISalesService _salesService;

    public RestaurantService(IApplicationDbContext context, ISalesService salesService)
    {
        _context = context;
        _salesService = salesService;
    }

    // ---------- Tables ----------

    public async Task<List<RestaurantTableDto>> GetTablesAsync(CancellationToken ct = default)
    {
        var tables = await _context.RestaurantTables.AsNoTracking().OrderBy(t => t.Number).ToListAsync(ct);
        return tables.Select(Map).ToList();
    }

    public async Task<RestaurantTableDto> CreateTableAsync(CreateRestaurantTableDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Number))
            throw new ValidationAppException("رقم الطاولة مطلوب.");
        if (await _context.RestaurantTables.AnyAsync(t => t.Number == dto.Number, ct))
            throw new ValidationAppException("رقم الطاولة ده مستخدم قبل كده.");

        var table = new RestaurantTable
        {
            Number = dto.Number,
            SectionName = dto.SectionName,
            Capacity = dto.Capacity,
            Status = RestaurantTableStatus.Available
        };
        _context.RestaurantTables.Add(table);
        await _context.SaveChangesAsync(ct);

        return Map(table);
    }

    public async Task<RestaurantTableDto> SetTableStatusAsync(Guid tableId, RestaurantTableStatus status, CancellationToken ct = default)
    {
        var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new NotFoundException(nameof(RestaurantTable), tableId);

        if (table.Status == RestaurantTableStatus.Occupied)
            throw new ValidationAppException("الطاولة دي مشغولة بطلب مفتوح — مينفعش تتغير حالتها يدويًا.");

        table.Status = status;
        await _context.SaveChangesAsync(ct);
        return Map(table);
    }

    // ---------- Menu ----------

    public async Task<List<MenuItemDto>> GetMenuAsync(CancellationToken ct = default)
    {
        var items = await _context.Items
            .AsNoTracking()
            .Include(i => i.ItemCategory)
            .Include(i => i.RecipeLines)
            .Where(i => i.IsMenuItem && i.IsActive && !i.IsDeleted)
            .OrderBy(i => i.NameAr)
            .ToListAsync(ct);

        return items.Select(i => new MenuItemDto
        {
            Id = i.Id,
            Code = i.Code,
            NameAr = i.NameAr,
            NameEn = i.NameEn,
            MenuPrice = i.MenuPrice,
            ItemCategoryId = i.ItemCategoryId,
            CategoryName = i.ItemCategory?.NameAr ?? string.Empty,
            HasRecipe = i.RecipeLines.Count > 0
        }).ToList();
    }

    public async Task<List<RecipeLineDto>> GetRecipeAsync(Guid itemId, CancellationToken ct = default)
    {
        var lines = await _context.MenuRecipeLines
            .AsNoTracking()
            .Include(l => l.RawMaterialItem)
            .Where(l => l.MenuItemId == itemId)
            .ToListAsync(ct);

        return lines.Select(l => new RecipeLineDto
        {
            RawMaterialItemId = l.RawMaterialItemId,
            RawMaterialCode = l.RawMaterialItem?.Code ?? string.Empty,
            RawMaterialName = l.RawMaterialItem?.NameAr ?? string.Empty,
            QuantityPerUnit = l.QuantityPerUnit
        }).ToList();
    }

    public async Task SetMenuItemAsync(Guid itemId, SetMenuItemDto dto, CancellationToken ct = default)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), itemId);

        if (dto.MenuPrice < 0)
            throw new ValidationAppException("سعر المنيو لا يمكن أن يكون سالبًا.");

        var rawMaterialIds = dto.RecipeLines.Select(l => l.RawMaterialItemId).ToList();
        if (rawMaterialIds.Distinct().Count() != rawMaterialIds.Count)
            throw new ValidationAppException("مفيش داعي لتكرار نفس المكوّن أكتر من مرة في نفس الوصفة.");
        if (rawMaterialIds.Contains(itemId))
            throw new ValidationAppException("الصنف مينفعش يكون مكوّن في وصفته هو نفسه.");

        foreach (var line in dto.RecipeLines)
        {
            if (line.QuantityPerUnit <= 0)
                throw new ValidationAppException("كمية المكوّن لكل وحدة يجب أن تكون أكبر من صفر.");
        }

        if (rawMaterialIds.Count > 0)
        {
            var existingCount = await _context.Items.CountAsync(i => rawMaterialIds.Contains(i.Id) && !i.IsDeleted, ct);
            if (existingCount != rawMaterialIds.Distinct().Count())
                throw new ValidationAppException("في مكوّن (صنف مخزون) غير موجود في القائمة.");
        }

        item.IsMenuItem = dto.IsMenuItem;
        item.MenuPrice = dto.MenuPrice;

        var existingLines = await _context.MenuRecipeLines.Where(l => l.MenuItemId == itemId).ToListAsync(ct);
        _context.MenuRecipeLines.RemoveRange(existingLines);

        foreach (var line in dto.RecipeLines)
        {
            _context.MenuRecipeLines.Add(new MenuRecipeLine
            {
                MenuItemId = itemId,
                RawMaterialItemId = line.RawMaterialItemId,
                QuantityPerUnit = line.QuantityPerUnit
            });
        }

        await _context.SaveChangesAsync(ct);
    }

    // ---------- Orders ----------

    public async Task<List<RestaurantOrderDto>> GetOrdersAsync(bool includeClosed = false, CancellationToken ct = default)
    {
        var vatRate = await GetVatRateAsync(ct);
        var query = _context.RestaurantOrders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.WaiterEmployee)
            .Include(o => o.SalesInvoice)
            .Include(o => o.Lines).ThenInclude(l => l.Item)
            .AsQueryable();

        if (!includeClosed)
            query = query.Where(o => o.Status == RestaurantOrderStatus.Open);

        var orders = await query.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderNumber).ToListAsync(ct);
        return orders.Select(o => Map(o, vatRate)).ToList();
    }

    public async Task<RestaurantOrderDto> GetOrderAsync(Guid id, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(id, ct);
        var vatRate = await GetVatRateAsync(ct);
        return Map(order, vatRate);
    }

    public async Task<RestaurantOrderDto> CreateOrderAsync(CreateRestaurantOrderDto dto, CancellationToken ct = default)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Warehouse), dto.WarehouseId);

        RestaurantTable? table = null;
        if (dto.OrderType == RestaurantOrderType.DineIn)
        {
            if (dto.TableId is null)
                throw new ValidationAppException("لازم تحدد الطاولة لطلب صالة.");

            table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == dto.TableId && !t.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(RestaurantTable), dto.TableId.Value);
            if (table.Status != RestaurantTableStatus.Available)
                throw new ValidationAppException("الطاولة دي مش متاحة دلوقتي.");
        }
        else if (dto.TableId is not null)
        {
            throw new ValidationAppException("طلب تيك أواي أو دليفري مينفعش يترتبط بطاولة.");
        }

        if (dto.WaiterEmployeeId is { } waiterId
            && !await _context.Employees.AnyAsync(e => e.Id == waiterId && !e.IsDeleted, ct))
            throw new NotFoundException(nameof(Employee), waiterId);

        var count = await _context.RestaurantOrders.CountAsync(ct);
        var order = new RestaurantOrder
        {
            OrderNumber = $"RO-{(count + 1):D6}",
            OrderType = dto.OrderType,
            OrderDate = DateTime.UtcNow,
            TableId = table?.Id,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            DeliveryAddress = dto.DeliveryAddress,
            WaiterEmployeeId = dto.WaiterEmployeeId,
            WarehouseId = warehouse.Id,
            Notes = dto.Notes,
            Status = RestaurantOrderStatus.Open
        };
        _context.RestaurantOrders.Add(order);

        if (table is not null)
            table.Status = RestaurantTableStatus.Occupied;

        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(order.Id, ct);
    }

    public async Task<RestaurantOrderDto> AddLineAsync(Guid orderId, AddOrderLineDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        if (dto.Quantity <= 0)
            throw new ValidationAppException("الكمية يجب أن تكون أكبر من صفر.");

        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == dto.ItemId && i.IsMenuItem && i.IsActive && !i.IsDeleted, ct)
            ?? throw new ValidationAppException("الصنف ده مش موجود في المنيو.");

        var nextLineNumber = order.Lines.Count == 0 ? 1 : order.Lines.Max(l => l.LineNumber) + 1;
        // Added directly to the DbSet (rather than via order.Lines.Add(...)) because EF's change tracker
        // can misdetect a new child appended to an already-loaded collection on a previously-saved,
        // re-queried parent as Modified instead of Added — adding to the DbSet directly is unambiguous.
        _context.RestaurantOrderLines.Add(new RestaurantOrderLine
        {
            RestaurantOrderId = order.Id,
            LineNumber = nextLineNumber,
            ItemId = item.Id,
            Quantity = dto.Quantity,
            UnitPrice = item.MenuPrice,
            LineTotal = Math.Round(dto.Quantity * item.MenuPrice, 2),
            Notes = dto.Notes
        });

        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    public async Task<RestaurantOrderDto> UpdateLineQuantityAsync(Guid orderId, Guid lineId, UpdateOrderLineQuantityDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new NotFoundException(nameof(RestaurantOrderLine), lineId);

        if (dto.Quantity <= 0)
        {
            _context.RestaurantOrderLines.Remove(line);
        }
        else
        {
            line.Quantity = dto.Quantity;
            line.LineTotal = Math.Round(dto.Quantity * line.UnitPrice, 2);
        }

        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    public async Task<RestaurantOrderDto> RemoveLineAsync(Guid orderId, Guid lineId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new NotFoundException(nameof(RestaurantOrderLine), lineId);

        _context.RestaurantOrderLines.Remove(line);
        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    public async Task<RestaurantOrderDto> CancelOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        order.Status = RestaurantOrderStatus.Cancelled;
        if (order.TableId is { } tableId)
        {
            var table = await _context.RestaurantTables.FirstAsync(t => t.Id == tableId, ct);
            table.Status = RestaurantTableStatus.Available;
        }

        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    /// <summary>Converts the order into a real SalesInvoice via ISalesService (walk-in customer, Cash/Card
    /// settlement, revenue/VAT/stock handled there for any non-recipe menu item), then separately posts the
    /// recipe-based raw-material consumption (stock issue + one combined COGS journal entry) for every line
    /// whose menu item has a recipe — mirroring SalesService's own item-line COGS posting pattern rather than
    /// duplicating its revenue/tax logic.</summary>
    public async Task<RestaurantOrderDto> BillOrderAsync(Guid orderId, BillOrderDto dto, CancellationToken ct = default)
    {
        if (dto.PaymentTerm != PaymentTerm.Cash && dto.PaymentTerm != PaymentTerm.Card && dto.PaymentTerm != PaymentTerm.Credit)
            throw new ValidationAppException("فواتير المطعم بتتحصل كاش أو شبكة أو آجل (لطلبات التوصيل بس).");

        var order = await LoadOrderAsync(orderId, ct);

        if (dto.PaymentTerm == PaymentTerm.Credit)
        {
            if (order.OrderType != RestaurantOrderType.Delivery)
                throw new ValidationAppException("التحصيل الآجل متاح بس لطلبات الدليفري.");
            if (string.IsNullOrWhiteSpace(dto.DeliveryPlatformName))
                throw new ValidationAppException("لازم تكتب اسم منصة التوصيل عشان تحصّل آجل.");
        }

        EnsureOpen(order);
        if (order.Lines.Count == 0)
            throw new ValidationAppException("لازم يكون في بند واحد على الأقل في الطلب قبل التحصيل.");

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);
        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن تحصيل طلب لفترة محاسبية مقفلة.");

        if (dto.CashierShiftId is { } shiftId)
        {
            var shift = await _context.CashierShifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct)
                ?? throw new NotFoundException(nameof(CashierShift), shiftId);
            if (shift.Status != CashierShiftStatus.Open)
                throw new ValidationAppException("شيفت الكاشير ده مقفول، لازم تفتح شيفت جديد.");
        }

        // Aggregate recipe-based ingredient consumption across the whole order and validate stock BEFORE
        // creating the invoice, so a shortage fails cleanly with nothing committed.
        var consumption = new Dictionary<Guid, (Item Item, decimal Quantity)>();
        foreach (var line in order.Lines.Where(l => l.Item!.RecipeLines.Count > 0))
        {
            foreach (var recipeLine in line.Item!.RecipeLines)
            {
                var qty = recipeLine.QuantityPerUnit * line.Quantity;
                consumption[recipeLine.RawMaterialItemId] = consumption.TryGetValue(recipeLine.RawMaterialItemId, out var existing)
                    ? (existing.Item, existing.Quantity + qty)
                    : (recipeLine.RawMaterialItem!, qty);
            }
        }

        foreach (var (item, qty) in consumption.Values)
        {
            if (qty > item.QuantityOnHand)
                throw new ValidationAppException($"الكمية المطلوبة من مكوّن ({item.Code} - {item.NameAr}) أكبر من الرصيد المتاح ({item.QuantityOnHand}).");
        }

        var customer = dto.PaymentTerm == PaymentTerm.Credit
            ? await GetOrCreatePlatformCustomerAsync(dto.DeliveryPlatformName!, ct)
            : await GetOrCreateWalkInCustomerAsync(ct);

        var invoiceLines = order.Lines.Select(l => new SalesInvoiceLineInputDto
        {
            Description = string.IsNullOrWhiteSpace(l.Notes) ? l.Item!.NameAr : $"{l.Item!.NameAr} ({l.Notes})",
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            // Recipe-based items go through as service lines (no ItemId) so ISalesService doesn't also try
            // to decrement the finished-product Item itself — only its non-recipe raw materials, below.
            ItemId = l.Item!.RecipeLines.Count > 0 ? null : l.ItemId
        }).ToList();

        var orderLabel = order.OrderType switch
        {
            RestaurantOrderType.DineIn => $"طاولة {order.Table?.Number}",
            RestaurantOrderType.Takeaway => "تيك أواي" + (string.IsNullOrWhiteSpace(order.CustomerName) ? "" : $" - {order.CustomerName}"),
            _ => "دليفري" + (string.IsNullOrWhiteSpace(order.CustomerName) ? "" : $" - {order.CustomerName}")
        };

        var invoice = await _salesService.CreateInvoiceAsync(new CreateSalesInvoiceDto
        {
            CustomerId = customer.Id,
            InvoiceDate = DateTime.UtcNow.Date,
            FiscalPeriodId = dto.FiscalPeriodId,
            PaymentTerm = dto.PaymentTerm,
            WarehouseId = order.WarehouseId,
            Notes = $"طلب مطعم {order.OrderNumber} - {orderLabel}",
            Lines = invoiceLines
        }, ct);

        if (consumption.Count > 0)
        {
            var cogsAccount = await GetAccountAsync(AccountingConstants.CostOfGoodsSoldAccountCode, "تكلفة البضاعة المباعة", ct);
            var inventoryAccount = await GetAccountAsync(AccountingConstants.InventoryAccountCode, "المخزون", ct);

            var movementSequenceBase = await _context.StockMovements.CountAsync(ct);
            decimal totalCogs = 0;
            var movementIndex = 0;
            var stockMovements = new List<StockMovement>();

            foreach (var (item, qty) in consumption.Values)
            {
                var unitCost = item.AverageCost;
                var movementCost = Math.Round(qty * unitCost, 2);
                totalCogs += movementCost;
                item.QuantityOnHand -= qty;

                stockMovements.Add(new StockMovement
                {
                    MovementNumber = $"SM-{(movementSequenceBase + ++movementIndex):D6}",
                    MovementDate = DateTime.UtcNow.Date,
                    MovementType = StockMovementType.Issue,
                    ItemId = item.Id,
                    WarehouseId = order.WarehouseId,
                    Quantity = qty,
                    UnitCost = unitCost,
                    TotalCost = movementCost,
                    Reference = order.OrderNumber,
                    Description = $"استهلاك وصفة لطلب مطعم {order.OrderNumber}"
                });
            }

            if (totalCogs > 0)
            {
                var cogsEntry = await SimpleJournalEntryFactory.CreatePostedAsync(
                    _context, DateTime.UtcNow.Date, dto.FiscalPeriodId,
                    $"تكلفة مكوّنات - طلب مطعم {order.OrderNumber}",
                    debitAccountId: cogsAccount.Id, creditAccountId: inventoryAccount.Id,
                    amount: totalCogs, reference: RestaurantConstants.RestaurantOrderReference, ct: ct);

                foreach (var movement in stockMovements)
                    movement.JournalEntry = cogsEntry;
            }

            _context.StockMovements.AddRange(stockMovements);
        }

        order.Status = RestaurantOrderStatus.Billed;
        order.SalesInvoiceId = invoice.Id;
        order.CashierShiftId = dto.CashierShiftId;
        if (order.TableId is { } tableId)
        {
            var table = await _context.RestaurantTables.FirstAsync(t => t.Id == tableId, ct);
            table.Status = RestaurantTableStatus.Available;
        }

        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(order.Id, ct);
    }

    private async Task<Customer> GetOrCreateWalkInCustomerAsync(CancellationToken ct)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Code == RestaurantConstants.WalkInCustomerCode, ct);
        if (customer is not null)
            return customer;

        customer = new Customer
        {
            Code = RestaurantConstants.WalkInCustomerCode,
            NameAr = RestaurantConstants.WalkInCustomerNameAr,
            NameEn = RestaurantConstants.WalkInCustomerNameEn,
            IsActive = true
        };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(ct);
        return customer;
    }

    /// <summary>Finds or creates the Customer record a delivery platform (HungerStation, Talabat, ...) bills
    /// under, matched by name (trimmed) among existing platform customers — each platform gets its own AR
    /// balance instead of sharing the generic walk-in customer, so what each platform owes stays separate
    /// and shows up on its own row in AR aging.</summary>
    private async Task<Customer> GetOrCreatePlatformCustomerAsync(string platformName, CancellationToken ct)
    {
        var name = platformName.Trim();
        var existing = await _context.Customers
            .Where(c => c.Code.StartsWith(RestaurantConstants.DeliveryPlatformCustomerCodePrefix))
            .FirstOrDefaultAsync(c => c.NameAr == name, ct);
        if (existing is not null)
            return existing;

        var count = await _context.Customers
            .CountAsync(c => c.Code.StartsWith(RestaurantConstants.DeliveryPlatformCustomerCodePrefix), ct);

        var customer = new Customer
        {
            Code = $"{RestaurantConstants.DeliveryPlatformCustomerCodePrefix}{count + 1:D3}",
            NameAr = name,
            NameEn = name,
            IsActive = true
        };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(ct);
        return customer;
    }

    private static void EnsureOpen(RestaurantOrder order)
    {
        if (order.Status != RestaurantOrderStatus.Open)
            throw new ValidationAppException("الطلب ده مقفول، مفيش تعديل ممكن عليه.");
    }

    private async Task<RestaurantOrder> LoadOrderAsync(Guid id, CancellationToken ct)
        => await _context.RestaurantOrders
            .Include(o => o.Table)
            .Include(o => o.WaiterEmployee)
            .Include(o => o.SalesInvoice)
            .Include(o => o.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.RecipeLines).ThenInclude(r => r.RawMaterialItem)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException(nameof(RestaurantOrder), id);

    private async Task<decimal> GetVatRateAsync(CancellationToken ct)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return settings?.VatRate ?? 0;
    }

    private async Task<Account> GetAccountAsync(string code, string arabicLabel, CancellationToken ct)
        => await _context.Accounts.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب {arabicLabel} ({code}) غير موجود في دليل الحسابات.");

    private static RestaurantTableDto Map(RestaurantTable t) => new()
    {
        Id = t.Id,
        Number = t.Number,
        SectionName = t.SectionName,
        Capacity = t.Capacity,
        Status = t.Status
    };

    private static RestaurantOrderDto Map(RestaurantOrder o, decimal vatRate)
    {
        var subTotal = o.Lines.Sum(l => l.LineTotal);
        var vatAmount = Math.Round(subTotal * vatRate, 2);

        return new RestaurantOrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            OrderType = o.OrderType,
            OrderDate = o.OrderDate,
            TableId = o.TableId,
            TableNumber = o.Table?.Number,
            CustomerName = o.CustomerName,
            CustomerPhone = o.CustomerPhone,
            DeliveryAddress = o.DeliveryAddress,
            WaiterEmployeeId = o.WaiterEmployeeId,
            WaiterName = o.WaiterEmployee?.FullNameAr,
            WarehouseId = o.WarehouseId,
            Status = o.Status,
            Notes = o.Notes,
            SalesInvoiceId = o.SalesInvoiceId,
            SalesInvoiceNumber = o.SalesInvoice?.InvoiceNumber,
            SubTotal = subTotal,
            VatRate = vatRate,
            VatAmount = vatAmount,
            TotalAmount = subTotal + vatAmount,
            Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new RestaurantOrderLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                ItemId = l.ItemId,
                ItemName = l.Item?.NameAr ?? string.Empty,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                Notes = l.Notes
            }).ToList()
        };
    }
}
