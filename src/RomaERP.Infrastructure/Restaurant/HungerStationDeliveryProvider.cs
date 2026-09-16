namespace RomaERP.Infrastructure.Restaurant;

/// <summary>Inactive until this tenant sets a HungerStation webhook secret (Delivery Platforms page) —
/// HungerStation only issues real credentials once a restaurant enrolls in its POS-integration partner
/// program (see IDeliveryPlatformProvider).</summary>
public class HungerStationDeliveryProvider : DeliveryPlatformProviderBase
{
    public HungerStationDeliveryProvider() : base("HungerStation")
    {
    }
}
