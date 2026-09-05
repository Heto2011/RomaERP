namespace RomaERP.Application.Common.Interfaces;

public record PaymentChargeRequest(
    Guid TenantId,
    decimal Amount,
    string Currency,
    string? CustomerRef,
    string? TokenRef,
    string Description);

public record PaymentChargeResult(bool Success, string? ProviderReference, string? FailureReason);

/// <summary>A pluggable card-charging gateway. A subscription only uses one of these once it has a saved
/// <c>PaymentProviderTokenRef</c> — until then billing stays on "Manual" (admin records bank transfers by
/// hand), so the whole subscription system works before any gateway account exists.</summary>
public interface IPaymentGatewayProvider
{
    /// <summary>Matches <see cref="Domain.Tenancy.Subscription.PaymentProvider"/>, e.g. "Moyasar".</summary>
    string Name { get; }
    bool IsConfigured { get; }
    Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default);
}
