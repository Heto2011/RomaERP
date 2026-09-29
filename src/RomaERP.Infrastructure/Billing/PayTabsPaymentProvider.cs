using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Billing;

/// <summary>Charges a previously-saved card token through PayTabs (paytabs.com) — chosen over a Saudi-only
/// gateway (e.g. Moyasar) because PayTabs can be registered under an Egyptian merchant profile
/// (PayTabs:ProfileId on the Egypt region endpoint) while still accepting international Visa/Mastercard
/// payments from Saudi customers, whereas a Saudi gateway needs a Saudi CR + Saudi IBAN just to onboard.
/// Inactive until PayTabs:ServerKey and PayTabs:ProfileId are set in configuration, so the rest of the
/// subscription system works in "Manual" mode (admin records bank transfers by hand) before a gateway
/// account exists.
///
/// Known limit of an Egypt-registered profile: a mada-only Saudi debit card (no Visa/Mastercard co-badge)
/// cannot be charged through it — that customer still pays by bank transfer ("Manual" mode) until the
/// business registers in Saudi Arabia and a native KSA-region PayTabs (or Moyasar) profile is added
/// alongside this one, which needs no code change beyond selecting that provider per subscription.</summary>
public class PayTabsPaymentProvider : IPaymentGatewayProvider
{
    private const string BaseUrl = "https://secure-egypt.paytabs.com/payment/";

    private readonly HttpClient _http;
    private readonly string? _serverKey;
    private readonly string? _profileId;

    public PayTabsPaymentProvider(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
        _serverKey = configuration["PayTabs:ServerKey"];
        _profileId = configuration["PayTabs:ProfileId"];
    }

    public string Name => "PayTabs";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_serverKey) && !string.IsNullOrWhiteSpace(_profileId);

    public async Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new PaymentChargeResult(false, null, "PayTabs غير مفعّل — لسه مفيش PayTabs:ServerKey و PayTabs:ProfileId في الإعدادات.");

        if (string.IsNullOrWhiteSpace(request.TokenRef))
            return new PaymentChargeResult(false, null, "مفيش بطاقة محفوظة لهذا الاشتراك.");

        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", _serverKey);

        // "recurring" tells PayTabs this is a follow-on charge against a token saved from an earlier
        // customer-present transaction, not a fresh checkout — matches how this provider is only ever
        // invoked (see IPaymentGatewayProvider's doc comment: only once a saved TokenRef exists).
        var payload = new PayTabsChargeRequest(
            ProfileId: _profileId!,
            TranType: "sale",
            TranClass: "recurring",
            CartId: Guid.NewGuid().ToString("N"),
            CartCurrency: request.Currency,
            CartAmount: request.Amount,
            CartDescription: request.Description,
            Token: request.TokenRef);

        try
        {
            var response = await _http.PostAsJsonAsync("request", payload, ct);
            var body = await response.Content.ReadFromJsonAsync<PayTabsChargeResponse>(cancellationToken: ct);

            // PayTabs response_status "A" = Authorised/settled successfully.
            if (response.IsSuccessStatusCode && body?.PaymentResult?.ResponseStatus == "A")
                return new PaymentChargeResult(true, body.TranRef, null);

            return new PaymentChargeResult(false, body?.TranRef, body?.PaymentResult?.ResponseMessage ?? $"PayTabs returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new PaymentChargeResult(false, null, ex.Message);
        }
    }

    private record PayTabsChargeRequest(
        [property: JsonPropertyName("profile_id")] string ProfileId,
        [property: JsonPropertyName("tran_type")] string TranType,
        [property: JsonPropertyName("tran_class")] string TranClass,
        [property: JsonPropertyName("cart_id")] string CartId,
        [property: JsonPropertyName("cart_currency")] string CartCurrency,
        [property: JsonPropertyName("cart_amount")] decimal CartAmount,
        [property: JsonPropertyName("cart_description")] string CartDescription,
        [property: JsonPropertyName("token")] string Token);

    private record PayTabsChargeResponse(
        [property: JsonPropertyName("tran_ref")] string? TranRef,
        [property: JsonPropertyName("payment_result")] PayTabsPaymentResult? PaymentResult);

    private record PayTabsPaymentResult(
        [property: JsonPropertyName("response_status")] string? ResponseStatus,
        [property: JsonPropertyName("response_message")] string? ResponseMessage);
}
