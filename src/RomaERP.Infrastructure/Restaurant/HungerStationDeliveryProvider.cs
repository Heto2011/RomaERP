using Microsoft.Extensions.Configuration;

namespace RomaERP.Infrastructure.Restaurant;

/// <summary>Inactive until HungerStation:WebhookSecret is set — HungerStation only issues real credentials
/// once a restaurant enrolls in its POS-integration partner program (see IDeliveryPlatformProvider).</summary>
public class HungerStationDeliveryProvider : DeliveryPlatformProviderBase
{
    public HungerStationDeliveryProvider(IConfiguration configuration) : base(configuration, "HungerStation", "HungerStation")
    {
    }
}
