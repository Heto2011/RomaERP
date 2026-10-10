namespace RomaERP.Application.Common.Interfaces;

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
}
