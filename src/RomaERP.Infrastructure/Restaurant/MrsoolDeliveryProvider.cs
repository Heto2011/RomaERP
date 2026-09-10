using Microsoft.Extensions.Configuration;

namespace RomaERP.Infrastructure.Restaurant;

/// <summary>Inactive until Mrsool:WebhookSecret is set — Mrsool has no public self-serve API, only access
/// through its merchant/POS-integration enrollment program (see IDeliveryPlatformProvider).</summary>
public class MrsoolDeliveryProvider : DeliveryPlatformProviderBase
{
    public MrsoolDeliveryProvider(IConfiguration configuration) : base(configuration, "Mrsool", "Mrsool")
    {
    }
}
