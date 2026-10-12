using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.Services;
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
    private readonly IItemLotService _lotService;
    private readonly IJournalEntryService _journalEntryService;

    public RestaurantService(IApplicationDbContext context, ISalesService salesService, IItemLotService lotService, IJournalEntryService journalEntryService)
    {
        _context = context;
        _salesService = salesService;
        _lotService = lotService;
        _journalEntryService = journalEntryService;
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
        var loyalty = await GetLoyaltySettingsAsync(ct);
        var query = _context.RestaurantOrders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.WaiterEmployee)
            .Include(o => o.SalesInvoice)
            .Include(o => o.LoyaltyMember)
            .Include(o => o.Lines).ThenInclude(l => l.Item)
            .AsQueryable();

        if (!includeClosed)
            query = query.Where(o => o.Status == RestaurantOrderStatus.Open);

        var orders = await query.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderNumber).ToListAsync(ct);
        return orders.Select(o => Map(o, vatRate, loyalty)).ToList();
    }

    public async Task<RestaurantOrderDto> GetOrderAsync(Guid id, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(id, ct);
        var vatRate = await GetVatRateAsync(ct);
        var loyalty = await GetLoyaltySettingsAsync(ct);
        return Map(order, vatRate, loyalty);
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

        // A returning customer (matched by phone) is attached automatically so their points and history follow the order.
        Guid? loyaltyMemberId = null;
        var orderPhone = LoyaltyPhone.Normalize(dto.CustomerPhone);
        if (orderPhone.Length >= 6 && (await GetLoyaltySettingsAsync(ct)).Enabled)
            loyaltyMemberId = await _context.LoyaltyMembers.Where(m => m.Phone == orderPhone).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);

        var count = await _context.RestaurantOrders.CountAsync(ct);
        var order = new RestaurantOrder
        {
            OrderNumber = $"RO-{(count + 1):D6}",
            LoyaltyMemberId = loyaltyMemberId,
            OrderType = dto.OrderType,
            OrderDate = DateTime.UtcNow,
            TableId = table?.Id,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            DeliveryAddress = dto.DeliveryAddress,
            WaiterEmployeeId = dto.WaiterEmployeeId,
            WarehouseId = warehouse.Id,
            Notes = dto.Notes,
            Status = RestaurantOrderStatus.Open,
            SourcePlatform = dto.SourcePlatform,
            ExternalOrderRef = dto.ExternalOrderRef
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

    public async Task<RestaurantOrderDto> SetLineDiscountAsync(Guid orderId, Guid lineId, SetLineDiscountDto dto, CancellationToken ct = default)
    {
        if (dto.DiscountAmount < 0)
            throw new ValidationAppException("قيمة الخصم لا يمكن أن تكون سالبة.");

        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new NotFoundException(nameof(RestaurantOrderLine), lineId);

        if (dto.DiscountAmount > line.LineTotal)
            throw new ValidationAppException("قيمة الخصم أكبر من إجمالي البند.");

        line.DiscountAmount = dto.DiscountAmount;
        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    public async Task<RestaurantOrderDto> SetOrderDiscountAsync(Guid orderId, SetOrderDiscountDto dto, CancellationToken ct = default)
    {
        if (dto.DiscountAmount < 0)
            throw new ValidationAppException("قيمة الخصم لا يمكن أن تكون سالبة.");

        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        var grossSubTotal = order.Lines.Sum(l => l.LineTotal - l.DiscountAmount);
        if (dto.DiscountAmount > grossSubTotal)
            throw new ValidationAppException("قيمة الخصم أكبر من إجمالي الطلب.");

        order.DiscountAmount = dto.DiscountAmount;
        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    /// <summary>Advances (or reverts) a line's kitchen ticket status — purely informational for the KDS,
    /// doesn't affect billing. Still requires the order to be Open (a Billed/Cancelled/Voided order has no
    /// kitchen work left to track).</summary>
    public async Task<RestaurantOrderDto> SetLineKitchenStatusAsync(Guid orderId, Guid lineId, SetLineKitchenStatusDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new NotFoundException(nameof(RestaurantOrderLine), lineId);

        line.KitchenStatus = dto.Status;
        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    /// <summary>"Split by item" — moves the given whole lines into a brand-new Open order (same table/type/
    /// customer/waiter/warehouse), so each half can be billed separately (e.g. two diners on one table asking
    /// for separate checks). Only whole lines move; splitting a single line's quantity across two checks isn't
    /// supported — the cashier adds it as two lines up front if that's needed. The table itself stays Occupied
    /// throughout, now backing two open orders instead of one.</summary>
    public async Task<SplitOrderResultDto> SplitOrderAsync(Guid orderId, SplitOrderDto dto, CancellationToken ct = default)
    {
        if (dto.LineIds is null || dto.LineIds.Count == 0)
            throw new ValidationAppException("لازم تحدد بند واحد على الأقل عشان تقسم الفاتورة.");

        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        var lineIds = dto.LineIds.Distinct().ToList();
        var linesToMove = order.Lines.Where(l => lineIds.Contains(l.Id)).ToList();
        if (linesToMove.Count != lineIds.Count)
            throw new ValidationAppException("في بند محدد مش موجود في الطلب ده.");
        if (linesToMove.Count == order.Lines.Count)
            throw new ValidationAppException("مينفعش تنقل كل بنود الطلب — لازم يفضل بند واحد على الأقل في الفاتورة الأصلية.");

        var count = await _context.RestaurantOrders.CountAsync(ct);
        var newOrder = new RestaurantOrder
        {
            OrderNumber = $"RO-{(count + 1):D6}",
            OrderType = order.OrderType,
            OrderDate = DateTime.UtcNow,
            TableId = order.TableId,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            DeliveryAddress = order.DeliveryAddress,
            WaiterEmployeeId = order.WaiterEmployeeId,
            WarehouseId = order.WarehouseId,
            LoyaltyMemberId = order.LoyaltyMemberId,
            Status = RestaurantOrderStatus.Open
        };
        _context.RestaurantOrders.Add(newOrder);

        var nextLineNumber = 1;
        foreach (var line in linesToMove)
        {
            line.RestaurantOrderId = newOrder.Id;
            line.LineNumber = nextLineNumber++;
        }

        await _context.SaveChangesAsync(ct);

        var originalDto = await GetOrderAsync(order.Id, ct);
        var newOrderDto = await GetOrderAsync(newOrder.Id, ct);
        return new SplitOrderResultDto { OriginalOrder = originalDto, NewOrder = newOrderDto };
    }

    /// <summary>Moves a whole open dine-in order to another table: the diners keep their order, the new table becomes occupied and the old one
    /// is freed (unless a split-off check is still open on it). The target must be free.</summary>
    public async Task<RestaurantOrderDto> MoveOrderToTableAsync(Guid orderId, MoveOrderTableDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);
        if (order.OrderType != RestaurantOrderType.DineIn || order.TableId is null)
            throw new ValidationAppException("نقل الطاولة متاح لطلبات الصالة بس.");
        if (order.TableId == dto.TargetTableId)
            throw new ValidationAppException("الطلب ده أصلًا على الطاولة دي.");

        var target = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == dto.TargetTableId && !t.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(RestaurantTable), dto.TargetTableId);
        if (target.Status != RestaurantTableStatus.Available)
            throw new ValidationAppException("الطاولة الجديدة مش متاحة دلوقتي.");

        var oldTableId = order.TableId;
        order.TableId = target.Id;
        target.Status = RestaurantTableStatus.Occupied;
        await _context.SaveChangesAsync(ct);

        await ReleaseTableIfIdleAsync(oldTableId, order.Id, ct);
        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(order.Id, ct);
    }

    /// <summary>Merges the source order into the target: every line moves over (renumbered after the target's own), the source's order-level
    /// discount is added to the target's, and the source order is closed with a note pointing to the target. Both must be open dine-in
    /// orders from the same warehouse (stock is issued from one place per order). The source's table is freed unless another check is open on it.</summary>
    public async Task<RestaurantOrderDto> MergeOrdersAsync(Guid targetOrderId, MergeOrdersDto dto, CancellationToken ct = default)
    {
        if (dto.SourceOrderId == targetOrderId)
            throw new ValidationAppException("مينفعش تدمج الطلب مع نفسه.");

        var target = await LoadOrderAsync(targetOrderId, ct);
        var source = await LoadOrderAsync(dto.SourceOrderId, ct);
        EnsureOpen(target);
        EnsureOpen(source);
        if (target.OrderType != RestaurantOrderType.DineIn || source.OrderType != RestaurantOrderType.DineIn)
            throw new ValidationAppException("الدمج متاح لطلبات الصالة بس.");
        if (target.WarehouseId != source.WarehouseId)
            throw new ValidationAppException("الطلبين لازم يكونوا على نفس المخزن.");

        var nextLineNumber = target.Lines.Count == 0 ? 1 : target.Lines.Max(l => l.LineNumber) + 1;
        foreach (var line in source.Lines.OrderBy(l => l.LineNumber).ToList())
        {
            line.RestaurantOrderId = target.Id;
            line.LineNumber = nextLineNumber++;
        }

        target.DiscountAmount += source.DiscountAmount;
        source.DiscountAmount = 0;
        if (target.LoyaltyMemberId is null && source.LoyaltyMemberId is not null)
        {
            target.LoyaltyMemberId = source.LoyaltyMemberId;
            target.LoyaltyPointsToRedeem = source.LoyaltyPointsToRedeem;
        }
        source.LoyaltyMemberId = null;
        source.LoyaltyPointsToRedeem = 0;
        source.Status = RestaurantOrderStatus.Cancelled;
        source.Notes = string.IsNullOrWhiteSpace(source.Notes) ? $"مدمج في {target.OrderNumber}" : $"{source.Notes} — مدمج في {target.OrderNumber}";
        await _context.SaveChangesAsync(ct);

        await ReleaseTableIfIdleAsync(source.TableId, source.Id, ct);
        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(target.Id, ct);
    }

    /// <summary>Attaches a repeat customer to the open order (found by phone, or registered on the spot) and, if asked, sets how many of
    /// their points to spend. The points are only deducted when the order is billed, so cancelling the order costs the customer nothing.</summary>
    public async Task<RestaurantOrderDto> SetOrderLoyaltyAsync(Guid orderId, SetOrderLoyaltyDto dto, CancellationToken ct = default)
    {
        var loyalty = await GetLoyaltySettingsAsync(ct);
        if (!loyalty.Enabled)
            throw new ValidationAppException("برنامج نقاط الولاء مش مفعّل. فعّله من إعدادات الولاء.");

        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        var phone = LoyaltyPhone.Normalize(dto.Phone);
        if (phone.Length < 6)
            throw new ValidationAppException("اكتب رقم موبايل صحيح للعميل.");

        var member = await _context.LoyaltyMembers.FirstOrDefaultAsync(m => m.Phone == phone, ct);
        if (member is null)
        {
            member = new LoyaltyMember
            {
                Phone = phone,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? $"عميل {phone}" : dto.Name.Trim()
            };
            _context.LoyaltyMembers.Add(member);
        }

        if (dto.PointsToRedeem < 0)
            throw new ValidationAppException("عدد النقاط لا يمكن أن يكون سالبًا.");
        if (dto.PointsToRedeem > 0)
        {
            if (dto.PointsToRedeem < loyalty.MinRedeemPoints)
                throw new ValidationAppException($"أقل عدد نقاط للاستبدال هو {loyalty.MinRedeemPoints}.");
            if (dto.PointsToRedeem > member.PointsBalance)
                throw new ValidationAppException($"رصيد النقاط ({member.PointsBalance}) أقل من المطلوب.");
        }

        order.LoyaltyMemberId = member.Id;
        order.LoyaltyMember = member;
        order.LoyaltyPointsToRedeem = dto.PointsToRedeem;
        if (string.IsNullOrWhiteSpace(order.CustomerName)) order.CustomerName = member.Name;
        if (string.IsNullOrWhiteSpace(order.CustomerPhone)) order.CustomerPhone = member.Phone;

        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    public async Task<RestaurantOrderDto> ClearOrderLoyaltyAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);
        order.LoyaltyMemberId = null;
        order.LoyaltyMember = null;
        order.LoyaltyPointsToRedeem = 0;
        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    /// <summary>A table goes back to Available only when no other open order is still seated at it (a split-off check keeps it occupied).</summary>
    private async Task ReleaseTableIfIdleAsync(Guid? tableId, Guid exceptOrderId, CancellationToken ct)
    {
        if (tableId is not { } id) return;
        var stillBusy = await _context.RestaurantOrders.AnyAsync(
            o => o.TableId == id && o.Id != exceptOrderId && o.Status == RestaurantOrderStatus.Open && !o.IsDeleted, ct);
        if (stillBusy) return;
        var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (table is not null) table.Status = RestaurantTableStatus.Available;
    }

    public async Task<RestaurantOrderDto> CancelOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        EnsureOpen(order);

        order.Status = RestaurantOrderStatus.Cancelled;
        await ReleaseTableIfIdleAsync(order.TableId, order.Id, ct);

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

        // Any order-level discount is spread proportionally across lines (by their own gross share) rather
        // than carried as its own negative invoice line, since SalesInvoiceLineInputDto rejects a negative
        // UnitPrice — every line's discounted price still ends up clamped to zero or above.
        var loyaltySettings = await GetLoyaltySettingsAsync(ct);
        var grossOrderTotal = order.Lines.Sum(l => l.LineTotal);
        var payableBeforeLoyalty = grossOrderTotal - order.Lines.Sum(l => l.DiscountAmount) - order.DiscountAmount;
        var (redeemedPoints, loyaltyDiscount) = LoyaltyRules.Redemption(order, payableBeforeLoyalty, loyaltySettings);
        if (redeemedPoints > 0 && order.LoyaltyMember!.PointsBalance < redeemedPoints)
            throw new ValidationAppException($"رصيد نقاط العميل ({order.LoyaltyMember.PointsBalance}) أقل من النقاط المطلوب استبدالها ({redeemedPoints}).");
        var totalOrderDiscount = order.DiscountAmount + loyaltyDiscount;

        var invoiceLines = order.Lines.Select(l =>
        {
            var orderDiscountShare = grossOrderTotal > 0
                ? Math.Round(totalOrderDiscount * (l.LineTotal / grossOrderTotal), 2)
                : 0;
            var netLineTotal = Math.Max(0, l.LineTotal - l.DiscountAmount - orderDiscountShare);
            var discountedUnitPrice = l.Quantity > 0 ? Math.Round(netLineTotal / l.Quantity, 2) : 0;

            return new SalesInvoiceLineInputDto
            {
                Description = string.IsNullOrWhiteSpace(l.Notes) ? l.Item!.NameAr : $"{l.Item!.NameAr} ({l.Notes})",
                Quantity = l.Quantity,
                UnitPrice = discountedUnitPrice,
                // Recipe-based items go through as service lines (no ItemId) so ISalesService doesn't also try
                // to decrement the finished-product Item itself — only its non-recipe raw materials, below.
                ItemId = l.Item!.RecipeLines.Count > 0 ? null : l.ItemId
            };
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

                await _lotService.ConsumeFefoAsync(item.Id, order.WarehouseId, qty, ct);

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
        ApplyLoyaltyOnBill(order, redeemedPoints, loyaltyDiscount, Math.Max(0m, payableBeforeLoyalty - loyaltyDiscount), loyaltySettings);
        await ReleaseTableIfIdleAsync(order.TableId, order.Id, ct);

        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(order.Id, ct);
    }

    /// <summary>Reverses a Billed order in full — revenue/VAT/settlement (Cash/Card/AR alike, whatever the
    /// original entry used) via the same journal-entry reversal used for manual entries, plus every stock
    /// issue tied to the order (both recipe-consumption movements referenced by the order number, and
    /// direct-item movements ISalesService posted under the invoice number) and the COGS entries behind
    /// them. Frees the cashier to immediately reopen a fresh order with the same lines for an "exchange"
    /// (the frontend does that by calling CreateOrderAsync/AddLineAsync again — no separate exchange
    /// endpoint is needed). Deliberately refuses a Credit-term order that already has a payment recorded,
    /// since only the invoice's own entry gets reversed, not any later partial-payment entries.</summary>
    public async Task<RestaurantOrderDto> VoidOrderAsync(Guid orderId, VoidOrderDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new ValidationAppException("لازم تكتب سبب الإلغاء.");

        var order = await LoadOrderAsync(orderId, ct);
        if (order.Status != RestaurantOrderStatus.Billed)
            throw new ValidationAppException("الإلغاء بعد التحصيل متاح بس للطلبات المحصّلة.");
        if (order.SalesInvoiceId is null)
            throw new ValidationAppException("الطلب ده مش مرتبط بفاتورة.");

        var invoice = await _context.SalesInvoices
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == order.SalesInvoiceId, ct)
            ?? throw new NotFoundException(nameof(SalesInvoice), order.SalesInvoiceId.Value);

        if (invoice.PaymentTerm == PaymentTerm.Credit && invoice.PaidAmount > 0)
            throw new ValidationAppException("الفاتورة دي اتحصل منها جزء بالفعل، مينفعش تتلغي بالطريقة دي.");

        if (invoice.JournalEntryId is { } mainEntryId)
            await _journalEntryService.ReverseAsync(mainEntryId, ct);

        if (invoice.PaymentTerm == PaymentTerm.Credit)
            invoice.Customer!.ArBalance -= (invoice.TotalAmount - invoice.PaidAmount);

        var issuedMovements = await _context.StockMovements
            .Where(m => m.MovementType == StockMovementType.Issue
                        && (m.Reference == order.OrderNumber || m.Reference == invoice.InvoiceNumber))
            .ToListAsync(ct);

        if (issuedMovements.Count > 0)
        {
            var itemIds = issuedMovements.Select(m => m.ItemId).Distinct().ToList();
            var itemsById = (await _context.Items.Where(i => itemIds.Contains(i.Id)).ToListAsync(ct)).ToDictionary(i => i.Id);

            var returnSequenceBase = await _context.StockMovements.CountAsync(ct);
            var returnMovements = new List<StockMovement>();
            var cogsEntryIdsToReverse = new HashSet<Guid>();

            foreach (var (movement, idx) in issuedMovements.Select((m, i) => (m, i)))
            {
                if (itemsById.TryGetValue(movement.ItemId, out var item))
                    item.QuantityOnHand += movement.Quantity;

                returnMovements.Add(new StockMovement
                {
                    MovementNumber = $"SM-{(returnSequenceBase + idx + 1):D6}",
                    MovementDate = DateTime.UtcNow.Date,
                    MovementType = StockMovementType.Receipt,
                    ItemId = movement.ItemId,
                    WarehouseId = movement.WarehouseId,
                    Quantity = movement.Quantity,
                    UnitCost = movement.UnitCost,
                    TotalCost = movement.TotalCost,
                    Reference = order.OrderNumber,
                    Description = $"استرجاع مخزون - إلغاء طلب مطعم {order.OrderNumber}"
                });

                if (movement.JournalEntryId is { } cogsEntryId)
                    cogsEntryIdsToReverse.Add(cogsEntryId);
            }

            _context.StockMovements.AddRange(returnMovements);

            foreach (var entryId in cogsEntryIdsToReverse)
                await _journalEntryService.ReverseAsync(entryId, ct);
        }

        if (order.LoyaltyMember is { } loyaltyMember)
        {
            var net = Math.Max(0m, order.Lines.Sum(l => l.LineTotal - l.DiscountAmount) - order.DiscountAmount - order.LoyaltyDiscountAmount);
            if (order.LoyaltyPointsToRedeem > 0)
            {
                loyaltyMember.PointsBalance += order.LoyaltyPointsToRedeem;
                AddLoyaltyTransaction(loyaltyMember, order, LoyaltyTransactionType.Reverse, order.LoyaltyPointsToRedeem, $"إرجاع نقاط مستبدلة - إلغاء {order.OrderNumber}");
            }
            if (order.LoyaltyPointsEarned > 0)
            {
                var taken = Math.Min(order.LoyaltyPointsEarned, loyaltyMember.PointsBalance);
                loyaltyMember.PointsBalance -= taken;
                loyaltyMember.LifetimePointsEarned = Math.Max(0, loyaltyMember.LifetimePointsEarned - order.LoyaltyPointsEarned);
                AddLoyaltyTransaction(loyaltyMember, order, LoyaltyTransactionType.Reverse, -taken, $"خصم نقاط مكتسبة - إلغاء {order.OrderNumber}");
            }
            loyaltyMember.VisitCount = Math.Max(0, loyaltyMember.VisitCount - 1);
            loyaltyMember.TotalSpent = Math.Max(0m, loyaltyMember.TotalSpent - net);
        }

        order.Status = RestaurantOrderStatus.Voided;
        order.VoidedAtUtc = DateTime.UtcNow;
        order.VoidReason = dto.Reason;

        await _context.SaveChangesAsync(ct);
        return await GetOrderAsync(orderId, ct);
    }

    /// <summary>Freezes the redemption on the order, spends the points and credits the ones just earned, and updates the customer's visit stats.</summary>
    private void ApplyLoyaltyOnBill(RestaurantOrder order, int redeemedPoints, decimal loyaltyDiscount, decimal netAmount, LoyaltySettings settings)
    {
        order.LoyaltyPointsToRedeem = redeemedPoints;
        order.LoyaltyDiscountAmount = loyaltyDiscount;
        order.LoyaltyPointsEarned = 0;

        var member = order.LoyaltyMember;
        if (member is null) return;

        var earned = LoyaltyRules.PointsEarned(netAmount, settings);
        order.LoyaltyPointsEarned = earned;

        if (redeemedPoints > 0)
        {
            member.PointsBalance -= redeemedPoints;
            AddLoyaltyTransaction(member, order, LoyaltyTransactionType.Redeem, -redeemedPoints, $"استبدال نقاط - {order.OrderNumber}");
        }
        if (earned > 0)
        {
            member.PointsBalance += earned;
            member.LifetimePointsEarned += earned;
            AddLoyaltyTransaction(member, order, LoyaltyTransactionType.Earn, earned, $"نقاط من الطلب {order.OrderNumber}");
        }
        member.VisitCount++;
        member.TotalSpent += netAmount;
        member.LastVisitUtc = DateTime.UtcNow;
    }

    private void AddLoyaltyTransaction(LoyaltyMember member, RestaurantOrder order, LoyaltyTransactionType type, int points, string note)
        => _context.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyMemberId = member.Id,
            RestaurantOrderId = order.Id,
            Type = type,
            Points = points,
            BalanceAfter = member.PointsBalance,
            Note = note
        });

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
            .Include(o => o.LoyaltyMember)
            .Include(o => o.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.RecipeLines).ThenInclude(r => r.RawMaterialItem)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException(nameof(RestaurantOrder), id);

    private async Task<decimal> GetVatRateAsync(CancellationToken ct)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return settings?.VatRate ?? 0;
    }

    private async Task<LoyaltySettings> GetLoyaltySettingsAsync(CancellationToken ct)
    {
        var s = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return s is null
            ? new LoyaltySettings(false, 10m, 0.5m, 20)
            : new LoyaltySettings(s.LoyaltyEnabled, s.LoyaltySpendPerPoint, s.LoyaltyPointValue, s.LoyaltyMinRedeemPoints);
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

    private static RestaurantOrderDto Map(RestaurantOrder o, decimal vatRate, LoyaltySettings loyalty)
    {
        var grossSubTotal = o.Lines.Sum(l => l.LineTotal);
        var lineDiscounts = o.Lines.Sum(l => l.DiscountAmount);

        // Once billed the redemption is frozen on the order; while open it follows the current lines and discounts.
        var (redeemPoints, loyaltyDiscount) = o.Status == RestaurantOrderStatus.Open
            ? LoyaltyRules.Redemption(o, grossSubTotal - lineDiscounts - o.DiscountAmount, loyalty)
            : (o.LoyaltyPointsToRedeem, o.LoyaltyDiscountAmount);
        var totalDiscount = lineDiscounts + o.DiscountAmount + loyaltyDiscount;
        var subTotal = Math.Max(0, grossSubTotal - totalDiscount);
        var pointsEarned = o.Status == RestaurantOrderStatus.Open
            ? (o.LoyaltyMemberId is null ? 0 : LoyaltyRules.PointsEarned(subTotal, loyalty))
            : o.LoyaltyPointsEarned;
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
            DiscountAmount = o.DiscountAmount,
            TotalDiscount = totalDiscount,
            GrossSubTotal = grossSubTotal,
            SubTotal = subTotal,
            VatRate = vatRate,
            VatAmount = vatAmount,
            TotalAmount = subTotal + vatAmount,
            VoidedAtUtc = o.VoidedAtUtc,
            VoidReason = o.VoidReason,
            SourcePlatform = o.SourcePlatform,
            ExternalOrderRef = o.ExternalOrderRef,
            LoyaltyMemberId = o.LoyaltyMemberId,
            LoyaltyMemberName = o.LoyaltyMember?.Name,
            LoyaltyMemberPhone = o.LoyaltyMember?.Phone,
            LoyaltyPointsBalance = o.LoyaltyMember?.PointsBalance ?? 0,
            LoyaltyPointsToRedeem = redeemPoints,
            LoyaltyDiscountAmount = loyaltyDiscount,
            LoyaltyPointsEarned = pointsEarned,
            Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new RestaurantOrderLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                ItemId = l.ItemId,
                ItemName = l.Item?.NameAr ?? string.Empty,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                DiscountAmount = l.DiscountAmount,
                Notes = l.Notes,
                KitchenStatus = l.KitchenStatus
            }).ToList()
        };
    }
}
