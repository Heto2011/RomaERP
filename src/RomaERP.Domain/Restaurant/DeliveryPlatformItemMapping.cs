using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;

namespace RomaERP.Domain.Restaurant;

/// <summary>Maps one delivery platform's own item identifier to a menu Item in this system, so an
/// incoming webhook order line can be resolved automatically. Set up once per menu item per platform —
/// an order line whose ExternalItemId has no mapping here fails intake rather than guessing (see
/// DeliveryOrderIntakeService), so nothing gets billed against the wrong item.</summary>
public class DeliveryPlatformItemMapping : AuditableEntity
{
    public string PlatformName { get; set; } = string.Empty;
    public string ExternalItemId { get; set; } = string.Empty;
    public string? ExternalItemName { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
}
