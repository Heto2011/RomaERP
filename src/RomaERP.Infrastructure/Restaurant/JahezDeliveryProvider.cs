namespace RomaERP.Infrastructure.Restaurant;

/// <summary>Inactive until this tenant sets a Jahez webhook secret (Delivery Platforms page) — Jahez only
/// issues real credentials (an API key and secret code) after emailing integration@jahez.net to request POS
/// integration (see IDeliveryPlatformProvider).</summary>
public class JahezDeliveryProvider : DeliveryPlatformProviderBase
{
    public JahezDeliveryProvider() : base("Jahez")
    {
    }
}
