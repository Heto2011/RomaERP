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
    decimal OutstandingAmount,
    string Currency,
    int OverdueDays,
    BillingPeriod BillingPeriod,
    // What the plan includes (null = unlimited) and what each extra costs per month in the tenant's currency, so the
    // customer can see where they stand against their limit and what going over costs.
    int? IncludedBranches = null,
    int? IncludedUsers = null,
    decimal ExtraBranchPrice = 0,
    decimal ExtraUserPrice = 0);

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
/// due invoices (with branch/user overage and multi-company discount, mirroring marketing/roma-erp.html),
/// attempting auto-charge where a real gateway is configured, and suspending tenants that stay unpaid past
/// the grace period. Not tenant-scoped — this is platform/central data.</summary>
public interface ISubscriptionBillingService
{
    Task<List<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct = default);
    Task<List<TenantSubscriptionDto>> GetTenantSubscriptionsAsync(CancellationToken ct = default);
    Task<TenantSubscriptionDto> SetPlanAsync(Guid tenantId, Guid planId, CancellationToken ct = default);
    Task<TenantSubscriptionDto> SetBillingAccountAsync(Guid tenantId, Guid? billingAccountId, CancellationToken ct = default);
    /// <summary>The owner confirms a customer's first payment: turns a trial tenant into a permanent paying one
    /// (clears the trial expiry), sets the plan, opens a one-month period and records that month's invoice as paid.</summary>
    Task<TenantSubscriptionDto> ActivatePaidAsync(Guid tenantId, Guid planId, string? paymentReference, BillingPeriod billingPeriod = BillingPeriod.Monthly, CancellationToken ct = default);
    /// <summary>Switches how the NEXT renewal is billed (monthly or one annual invoice). The period already paid is untouched.</summary>
    Task<TenantSubscriptionDto> SetBillingPeriodAsync(Guid tenantId, BillingPeriod billingPeriod, CancellationToken ct = default);
    Task<TenantSubscriptionDto> SuspendAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSubscriptionDto> ReactivateAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<SubscriptionInvoiceDto>> GetInvoicesAsync(Guid? tenantId, CancellationToken ct = default);
    Task<SubscriptionInvoiceDto> MarkInvoicePaidAsync(Guid invoiceId, string? paymentReference, CancellationToken ct = default);
    Task<BillingRunResultDto> RunBillingCycleAsync(CancellationToken ct = default);
}
