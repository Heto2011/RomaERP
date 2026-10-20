using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.DTOs;

public class ReservationDto
{
    public Guid Id { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public DateTime ReservationAt { get; set; }
    public Guid? TableId { get; set; }
    public string? TableNumber { get; set; }
    public ReservationStatus Status { get; set; }
    public string? Notes { get; set; }
    public Guid? RestaurantOrderId { get; set; }
    public string? OrderNumber { get; set; }
}

public class SaveReservationDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public DateTime ReservationAt { get; set; }
    public Guid? TableId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Turns a booked reservation into a running order. The guests choose dine-in or takeaway when they arrive;
/// dine-in uses the reserved table unless another one is given.</summary>
public class SeatReservationDto
{
    public RestaurantOrderType OrderType { get; set; } = RestaurantOrderType.DineIn;
    public Guid? TableId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? WaiterEmployeeId { get; set; }
}

public class SeatReservationResultDto
{
    public ReservationDto Reservation { get; set; } = new();
    public RestaurantOrderDto Order { get; set; } = new();
}
