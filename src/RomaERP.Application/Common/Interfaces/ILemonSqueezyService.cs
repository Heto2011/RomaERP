namespace RomaERP.Application.Common.Interfaces;

public enum ExtraKind { Branch, User }

/// <summary>Result of changing the number of paid extras: either a hosted checkout to pay for the first extra
/// (<see cref="CheckoutUrl"/>), or the new quantity when the existing add-on subscription was updated in place.</summary>
public record ExtraChangeResult(string? CheckoutUrl, int NewQuantity);

public record LemonCheckoutRequest(Guid TenantId, string CompanyCode, string Email, string Name, string PlanCode, bool Annual, string RedirectUrl, bool Uk = false);

/// <summary>Card payments through Lemon Squeezy (merchant of record) for customers outside Egypt. Lemon owns the
/// recurring charge; we start a hosted checkout for a plan and then follow Lemon's webhooks to activate the
/// subscription, record each payment as a paid invoice, and lock the company if the subscription expires.</summary>
public interface ILemonSqueezyService
{
    /// <summary>True once the API key and webhook secret are configured. Plan variants are found automatically from the
    /// products in the Lemon account (named "Roma ERP — Essential" ... with variants Monthly / Annual / UK Monthly / UK Annual).</summary>
    bool IsConfigured { get; }

    /// <summary>Whether a Lemon product variant exists for this plan and billing period. UK and Guernsey customers have
    /// their own (higher) USD price set, keyed with a "-uk" suffix; they never fall back to the Gulf prices.</summary>
    Task<bool> HasVariantAsync(string planCode, bool annual, bool uk = false, CancellationToken ct = default);

    /// <summary>Creates a hosted checkout and returns its URL. The tenant, plan and period travel as custom data
    /// and come back on every webhook, which is how a payment is tied to a company.</summary>
    Task<string> CreateCheckoutAsync(LemonCheckoutRequest request, CancellationToken ct = default);

    /// <summary>Constant-time check of Lemon's X-Signature header (HMAC-SHA256 of the raw body, hex).</summary>
    bool VerifySignature(string rawBody, string? signature);

    Task HandleWebhookAsync(string rawBody, CancellationToken ct = default);

    /// <summary>Adds or removes paid extras (branches or users/employees) on a card-paid company. The first extra needs a
    /// hosted checkout; after that the add-on subscription's quantity is changed in place and billed pro rata.
    /// <paramref name="minimumQuantity"/> is how many extras are actually in use, so nobody pays less than they use.</summary>
    Task<ExtraChangeResult> ChangeExtrasAsync(Guid tenantId, ExtraKind kind, int delta, int minimumQuantity, string email, string name, string redirectUrl, bool uk, CancellationToken ct = default);

    /// <summary>Moves a card-paid company to a higher plan right away (Lemon charges the difference pro rata).</summary>
    Task ChangePlanAsync(Guid tenantId, string newPlanCode, bool uk, CancellationToken ct = default);
}
