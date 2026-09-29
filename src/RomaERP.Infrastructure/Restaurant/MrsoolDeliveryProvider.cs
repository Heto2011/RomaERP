namespace RomaERP.Infrastructure.Restaurant;

/// <summary>Inactive until this tenant sets a Mrsool webhook secret (Delivery Platforms page) — Mrsool has
/// no public self-serve API, only access through its merchant/POS-integration enrollment program (see
/// IDeliveryPlatformProvider).</summary>
public class MrsoolDeliveryProvider : DeliveryPlatformProviderBase
{
    public MrsoolDeliveryProvider() : base("Mrsool")
    {
    }
}
