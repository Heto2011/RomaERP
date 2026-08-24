using RomaERP.Domain.Common;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Sales;

namespace RomaERP.Domain.Restaurant;

public enum RestaurantOrderType
{
    DineIn = 1,
    Takeaway = 2,
    Delivery = 3
}

public enum RestaurantOrderStatus
{
    Open = 1,
    Billed = 2,
    Cancelled = 3
}

/// <summary>A running order at a table (or a takeaway/delivery ticket) being built up before it's billed.
/// Billing converts it into a real SalesInvoice via ISalesService — this entity never posts to the GL itself.</summary>
public class RestaurantOrder : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public RestaurantOrderType OrderType { get; set; }
    public DateTime OrderDate { get; set; }

    /// <summary>Required for DineIn, must stay null for Takeaway/Delivery.</summary>
    public Guid? TableId { get; set; }
    public RestaurantTable? Table { get; set; }

    /// <summary>Free-text — Takeaway/Delivery orders aren't tied to a Customer record; they bill under a
    /// shared walk-in Customer, with these fields kept for the printed ticket/reference only.</summary>
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }

    public Guid? WaiterEmployeeId { get; set; }
    public Employee? WaiterEmployee { get; set; }

    /// <summary>Stock for every line (whether decremented directly or via a recipe) is issued from here.</summary>
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public RestaurantOrderStatus Status { get; set; } = RestaurantOrderStatus.Open;
    public string? Notes { get; set; }

    public Guid? SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    public ICollection<RestaurantOrderLine> Lines { get; set; } = new List<RestaurantOrderLine>();
}

public class RestaurantOrderLine : BaseEntity
{
    public Guid RestaurantOrderId { get; set; }
    public RestaurantOrder? RestaurantOrder { get; set; }

    public int LineNumber { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; } = 1;

    /// <summary>Snapshot of Item.MenuPrice at the moment this line was added — later menu price changes
    /// don't retroactively change an order already being built.</summary>
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public string? Notes { get; set; }
}
