using Microsoft.Extensions.Configuration;

namespace RomaERP.Infrastructure.Restaurant;

/// <summary>Inactive until Jahez:WebhookSecret is set — Jahez only issues real credentials (an API key and
/// secret code) after emailing integration@jahez.net to request POS integration (see
/// IDeliveryPlatformProvider).</summary>
public class JahezDeliveryProvider : DeliveryPlatformProviderBase
{
    public JahezDeliveryProvider(IConfiguration configuration) : base(configuration, "Jahez", "Jahez")
    {
    }
}
