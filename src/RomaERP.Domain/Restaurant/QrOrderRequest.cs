using RomaERP.Domain.Common;

namespace RomaERP.Domain.Restaurant;

public enum QrOrderStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

/// <summary>An order a guest sent from the phone after scanning the QR code on a table. It is only a request: nothing reaches the kitchen or the
/// books until staff approve it, which turns it into a normal restaurant order.</summary>
public class QrOrderRequest : AuditableEntity
{
    public Guid TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public QrOrderStatus Status { get; set; } = QrOrderStatus.Pending;
    /// <summary>How far the phone was from the restaurant when the order was sent.</summary>
    public int DistanceMeters { get; set; }
    /// <summary>A one-way hash of the guest's device and address, used only to limit how many requests one phone can send.</summary>
    public string DeviceKey { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid? RestaurantOrderId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedByUserId { get; set; }
    public List<QrOrderRequestLine> Lines { get; set; } = new();
}

public class QrOrderRequestLine : BaseEntity
{
    public Guid QrOrderRequestId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}
