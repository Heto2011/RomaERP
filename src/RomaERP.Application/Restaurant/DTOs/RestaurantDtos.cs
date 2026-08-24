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

public class RestaurantOrderLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
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
    public decimal SubTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<RestaurantOrderLineDto> Lines { get; set; } = new();
}

/// <summary>Cash/Card only — a walk-in restaurant order never extends credit to the shared placeholder
/// "walk-in customer" the way a real named B2B customer might.</summary>
public class BillOrderDto
{
    public PaymentTerm PaymentTerm { get; set; }
    public Guid FiscalPeriodId { get; set; }
}
