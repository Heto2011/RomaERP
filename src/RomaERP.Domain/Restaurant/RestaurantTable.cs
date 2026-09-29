using RomaERP.Domain.Common;

namespace RomaERP.Domain.Restaurant;

public enum RestaurantTableStatus
{
    Available = 1,
    Occupied = 2,
    Reserved = 3
}

public class RestaurantTable : AuditableEntity
{
    public string Number { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    public int Capacity { get; set; }
    public RestaurantTableStatus Status { get; set; } = RestaurantTableStatus.Available;
}
