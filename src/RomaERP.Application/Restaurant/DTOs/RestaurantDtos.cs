using RomaERP.Domain.Common;
using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.DTOs;

public class RestaurantTableDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    public int Capacity { get; set; }
    public RestaurantTableStatus Status { get; set; }
}

public class CreateRestaurantTableDto
{
    public string Number { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    public int Capacity { get; set; }
}

public class MenuItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal MenuPrice { get; set; }
    public Guid ItemCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool HasRecipe { get; set; }
}

public class RecipeLineDto
{
    public Guid RawMaterialItemId { get; set; }
    public string RawMaterialCode { get; set; } = string.Empty;
    public string RawMaterialName { get; set; } = string.Empty;
    public decimal QuantityPerUnit { get; set; }
}

public class SetRecipeLineInputDto
{
    public Guid RawMaterialItemId { get; set; }
    public decimal QuantityPerUnit { get; set; }
}

/// <summary>Marks an inventory Item as a menu item (or clears it) and replaces its recipe wholesale.
/// An empty RecipeLines list means the item is its own raw material — decremented directly on sale.</summary>
public class SetMenuItemDto
{
    public bool IsMenuItem { get; set; }
    public decimal MenuPrice { get; set; }
    public List<SetRecipeLineInputDto> RecipeLines { get; set; } = new();
}

public class CreateRestaurantOrderDto
{
    public RestaurantOrderType OrderType { get; set; }
    public Guid? TableId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public Guid? WaiterEmployeeId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set only by the delivery-webhook intake flow — never by the POS UI. See
    /// RestaurantOrder.SourcePlatform/ExternalOrderRef.</summary>
    public string? SourcePlatform { get; set; }
    public string? ExternalOrderRef { get; set; }
}

public class AddOrderLineDto
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string? Notes { get; set; }
}

public class UpdateOrderLineQuantityDto
{
    public decimal Quantity { get; set; }
}

/// <summary>A manual, cashier-entered discount off one order line — must not exceed that line's total.</summary>
public class SetLineDiscountDto
{
    public decimal DiscountAmount { get; set; }
}

/// <summary>A manual, cashier-entered discount off the whole order's total, on top of any per-line
/// discounts — must not exceed the order's gross subtotal.</summary>
public class SetOrderDiscountDto
{
    public decimal DiscountAmount { get; set; }
}

public class RestaurantOrderLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public KitchenLineStatus KitchenStatus { get; set; }
}

public class SetLineKitchenStatusDto
{
    public KitchenLineStatus Status { get; set; }
}

/// <summary>Moves the given whole lines out of an Open order into a brand-new Open order (same table/type/
/// customer), so each half can be billed separately — "split by item". The source order must keep at least
/// one line; the split-off set can't be all of them.</summary>
public class SplitOrderDto
{
    public List<Guid> LineIds { get; set; } = new();
}

public class SplitOrderResultDto
{
    public RestaurantOrderDto OriginalOrder { get; set; } = null!;
    public RestaurantOrderDto NewOrder { get; set; } = null!;
}

public class RestaurantOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public RestaurantOrderType OrderType { get; set; }
    public DateTime OrderDate { get; set; }
    public Guid? TableId { get; set; }
    public string? TableNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public Guid? WaiterEmployeeId { get; set; }
    public string? WaiterName { get; set; }
    public Guid WarehouseId { get; set; }
    public RestaurantOrderStatus Status { get; set; }
    public string? Notes { get; set; }
    public Guid? SalesInvoiceId { get; set; }
    public string? SalesInvoiceNumber { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>Sum of every line's own discount plus the order-level discount — the total taken off
    /// GrossSubTotal to reach SubTotal.</summary>
    public decimal TotalDiscount { get; set; }
    public decimal GrossSubTotal { get; set; }
    public decimal SubTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
    public string? SourcePlatform { get; set; }
    public string? ExternalOrderRef { get; set; }
    public List<RestaurantOrderLineDto> Lines { get; set; } = new();
}

/// <summary>Cash/Card settle immediately against the shared walk-in customer. Credit is only valid for a
/// Delivery order — the platform (HungerStation, Talabat, ...) collects from the diner and pays the
/// restaurant later, so DeliveryPlatformName is required in that case and routes the invoice to that
/// platform's own Customer record (tracked separately in AR) instead of the walk-in one.</summary>
public class BillOrderDto
{
    public PaymentTerm PaymentTerm { get; set; }
    public Guid FiscalPeriodId { get; set; }
    /// <summary>The cashier's currently open CashierShift, if any — links this sale to that shift's
    /// cash-drawer reconciliation. Optional so billing still works with no shift open.</summary>
    public Guid? CashierShiftId { get; set; }
    /// <summary>Required only when PaymentTerm is Credit — the delivery platform's name, used to find or
    /// create its own Customer record so each platform's amount owed is tracked separately.</summary>
    public string? DeliveryPlatformName { get; set; }
}

/// <summary>Reverses a Billed order in full: revenue/VAT/settlement (and AR, for a Credit sale) via the
/// standard journal-entry reversal, plus every stock issue tied to the order and its COGS. See
/// RestaurantService.VoidOrderAsync for the exact mechanics and its limits.</summary>
public class VoidOrderDto
{
    public string Reason { get; set; } = string.Empty;
}
