using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Common.Interfaces;

public record SubscriptionPlanDto(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    decimal MonthlyBasePrice,
    int IncludedBranches,
    int IncludedUsers,
    bool IsCustomPricing,
    bool IsActive);

public record TenantSubscriptionDto(
    Guid TenantId,
    string CompanyCode,
    string CompanyNameAr,
    string CompanyNameEn,
    bool TenantIsActive,
    Guid SubscriptionId,
    Guid PlanId,
    string PlanCode,
    string PlanNameAr,
    SubscriptionStatus Status,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    Guid? BillingAccountId,
    string PaymentProvider,
    int CurrentBranches,
    int CurrentUsers,
    decimal OutstandingAmount);

public record SubscriptionInvoiceDto(
    Guid Id,
    Guid TenantId,
    string CompanyNameAr,
    string PlanCode,
    string PlanNameAr,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal BaseAmount,
    int ExtraBranches,
    decimal ExtraBranchesAmount,
    int ExtraUsers,
    decimal ExtraUsersAmount,
    decimal MultiCompanyDiscountAmount,
    decimal TotalAmount,
    string Currency,
    SubscriptionInvoiceStatus Status,
    DateTime DueDateUtc,
    DateTime? PaidAtUtc,
    string? PaymentReference);

public record BillingRunResultDto(int InvoicesGenerated, int AutoCharged, int Suspended, List<string> Notes);

/// <summary>Owns the recurring monthly billing lifecycle for every tenant: plans, subscriptions, generating
/// due invoices (with branch/user overage and multi-company discount, mirroring marketing/pricing.html),
/// attempting auto-charge where a real gateway is configured, and suspending tenants that stay unpaid past
/// the grace period. Not tenant-scoped — this is platform/central data.</summary>
public interface ISubscriptionBillingService
{
    Task<List<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct = default);
    Task<List<TenantSubscriptionDto>> GetTenantSubscriptionsAsync(CancellationToken ct = default);
    Task<TenantSubscriptionDto> SetPlanAsync(Guid tenantId, Guid planId, CancellationToken ct = default);
    Task<TenantSubscriptionDto> SetBillingAccountAsync(Guid tenantId, Guid? billingAccountId, CancellationToken ct = default);
    Task<TenantSubscriptionDto> SuspendAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSubscriptionDto> ReactivateAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<SubscriptionInvoiceDto>> GetInvoicesAsync(Guid? tenantId, CancellationToken ct = default);
    Task<SubscriptionInvoiceDto> MarkInvoicePaidAsync(Guid invoiceId, string? paymentReference, CancellationToken ct = default);
    Task<BillingRunResultDto> RunBillingCycleAsync(CancellationToken ct = default);
}
