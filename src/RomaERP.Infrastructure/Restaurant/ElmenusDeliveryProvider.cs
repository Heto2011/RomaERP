namespace RomaERP.Infrastructure.Restaurant;

/// <summary>Ready but inactive until this tenant sets a Elmenus webhook secret (Delivery Platforms page). Elmenus only issues real credentials
/// to restaurants enrolled in its POS-integration partner programme; once the restaurant has them, entering the secret is all that is left
/// (see IDeliveryPlatformProvider for why the payload shape is a placeholder until that platform's real spec is in hand).</summary>
public class ElmenusDeliveryProvider : DeliveryPlatformProviderBase
{
    public ElmenusDeliveryProvider() : base("Elmenus")
    {
    }
}
