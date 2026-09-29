using RomaERP.Domain.Common;

namespace RomaERP.Domain.Restaurant;

/// <summary>Per-tenant webhook secret for one delivery platform (HungerStation/Jahez/Mrsool), each restaurant's
/// own value from that platform's POS-integration partner program — never shared across tenants. Kept in the
/// tenant's own database (this is a database-per-tenant deployment), so a webhook forged/replayed against one
/// tenant's company code can only ever verify against that tenant's own secret, never another tenant's.</summary>
public class DeliveryPlatformCredential : AuditableEntity
{
    public string PlatformName { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}
