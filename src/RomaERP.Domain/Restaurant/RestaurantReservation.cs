using RomaERP.Domain.Common;

namespace RomaERP.Domain.Restaurant;

public enum ReservationStatus
{
    Booked = 1,
    /// <summary>The guests arrived and the reservation was turned into a running order.</summary>
    Seated = 2,
    Cancelled = 3,
    NoShow = 4
}

/// <summary>A table booking taken before the guests arrive. When they show up it is converted into a
/// <see cref="RestaurantOrder"/> (dine-in or takeaway), and that order is later billed into a SalesInvoice.
/// The reservation never touches stock or the ledger itself.</summary>
public class RestaurantReservation : AuditableEntity
{
    public string ReservationNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int PartySize { get; set; }

    /// <summary>Restaurant-local date and time of arrival, as entered by staff (no time-zone conversion).</summary>
    public DateTime ReservationAt { get; set; }

    public Guid? TableId { get; set; }
    public RestaurantTable? Table { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Booked;
    public string? Notes { get; set; }

    /// <summary>The order created when the guests were seated.</summary>
    public Guid? RestaurantOrderId { get; set; }
    public RestaurantOrder? RestaurantOrder { get; set; }
}
