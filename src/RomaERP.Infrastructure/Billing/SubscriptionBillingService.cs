using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Identity;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.Infrastructure.Billing;

public class SubscriptionBillingService : ISubscriptionBillingService
{
    private readonly CentralDbContext _central;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantRegistry _registry;
    private readonly IReadOnlyDictionary<string, IPaymentGatewayProvider> _providers;

    public SubscriptionBillingService(
        CentralDbContext central,
        IServiceScopeFactory scopeFactory,
        ITenantRegistry registry,
        IEnumerable<IPaymentGatewayProvider> providers)
    {
        _central = central;
        _scopeFactory = scopeFactory;
        _registry = registry;
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct = default)
    {
        var plans = await _central.SubscriptionPlans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync(ct);
        return plans.Select(MapPlan).ToList();
    }

    public async Task<List<TenantSubscriptionDto>> GetTenantSubscriptionsAsync(CancellationToken ct = default)
    {
        var tenants = await _central.Tenants.AsNoTracking().OrderBy(t => t.CompanyNameEn).ToListAsync(ct);
        var result = new List<TenantSubscriptionDto>();

        foreach (var tenant in tenants)
        {
            var subscription = await GetOrCreateSubscriptionAsync(tenant, ct);
            var plan = await _central.SubscriptionPlans.AsNoTracking().FirstAsync(p => p.Id == subscription.PlanId, ct);
            var (branches, users) = await CountTenantUsageAsync(tenant, ct);
            var outstanding = await _central.SubscriptionInvoices.AsNoTracking()
                .Where(i => i.TenantId == tenant.Id && i.Status != SubscriptionInvoiceStatus.Paid && i.Status != SubscriptionInvoiceStatus.Cancelled)
                .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0;

            result.Add(MapTenantSubscription(tenant, subscription, plan, branches, users, outstanding));
        }

        return result;
    }

    public async Task<TenantSubscriptionDto> SetPlanAsync(Guid tenantId, Guid planId, CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);
        var plan = await _central.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), planId);
        var subscription = await GetOrCreateSubscriptionAsync(tenant, ct);

        subscription.PlanId = plan.Id;
        await _central.SaveChangesAsync(ct);

        return await BuildDtoAsync(tenant, subscription, plan, ct);
    }

    public async Task<TenantSubscriptionDto> SetBillingAccountAsync(Guid tenantId, Guid? billingAccountId, CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);
        var subscription = await GetOrCreateSubscriptionAsync(tenant, ct);

        subscription.BillingAccountId = billingAccountId;
        await _central.SaveChangesAsync(ct);

        var plan = await _central.SubscriptionPlans.AsNoTracking().FirstAsync(p => p.Id == subscription.PlanId, ct);
        return await BuildDtoAsync(tenant, subscription, plan, ct);
    }

    public async Task<TenantSubscriptionDto> SuspendAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);
        var subscription = await GetOrCreateSubscriptionAsync(tenant, ct);

        subscription.Status = SubscriptionStatus.Suspended;
        subscription.SuspendedAtUtc = DateTime.UtcNow;
        tenant.IsActive = false;
        await _central.SaveChangesAsync(ct);

        var plan = await _central.SubscriptionPlans.AsNoTracking().FirstAsync(p => p.Id == subscription.PlanId, ct);
        return await BuildDtoAsync(tenant, subscription, plan, ct);
    }

    public async Task<TenantSubscriptionDto> ReactivateAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);
        var subscription = await GetOrCreateSubscriptionAsync(tenant, ct);

        subscription.Status = SubscriptionStatus.Active;
        subscription.SuspendedAtUtc = null;
        tenant.IsActive = true;
        await _central.SaveChangesAsync(ct);

        var plan = await _central.SubscriptionPlans.AsNoTracking().FirstAsync(p => p.Id == subscription.PlanId, ct);
        return await BuildDtoAsync(tenant, subscription, plan, ct);
    }

    public async Task<List<SubscriptionInvoiceDto>> GetInvoicesAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var query = _central.SubscriptionInvoices.AsNoTracking().AsQueryable();
        if (tenantId.HasValue) query = query.Where(i => i.TenantId == tenantId.Value);

        var invoices = await query.OrderByDescending(i => i.DueDateUtc).ToListAsync(ct);
        var tenantNames = await _central.Tenants.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.CompanyNameAr, ct);

        return invoices.Select(i => MapInvoice(i, tenantNames.GetValueOrDefault(i.TenantId, "—"))).ToList();
    }

    public async Task<SubscriptionInvoiceDto> MarkInvoicePaidAsync(Guid invoiceId, string? paymentReference, CancellationToken ct = default)
    {
        var invoice = await _central.SubscriptionInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new NotFoundException(nameof(SubscriptionInvoice), invoiceId);

        invoice.Status = SubscriptionInvoiceStatus.Paid;
        invoice.PaidAtUtc = DateTime.UtcNow;
        invoice.PaymentReference = paymentReference;

        var subscription = await _central.Subscriptions.FirstOrDefaultAsync(s => s.Id == invoice.SubscriptionId, ct);
        if (subscription is not null && subscription.Status == SubscriptionStatus.PastDue)
            subscription.Status = SubscriptionStatus.Active;

        await _central.SaveChangesAsync(ct);

        var tenant = await _central.Tenants.AsNoTracking().FirstAsync(t => t.Id == invoice.TenantId, ct);
        return MapInvoice(invoice, tenant.CompanyNameAr);
    }

    public async Task<BillingRunResultDto> RunBillingCycleAsync(CancellationToken ct = default)
    {
        var notes = new List<string>();
        var today = DateTime.UtcNow;

        var dueSubscriptions = await _central.Subscriptions
            .Where(s => (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing || s.Status == SubscriptionStatus.PastDue)
                        && s.CurrentPeriodEnd <= today)
            .OrderBy(s => s.BillingAccountId).ThenBy(s => s.CreatedAtUtc)
            .ToListAsync(ct);

        var generated = 0;
        var autoCharged = 0;
        var seenBillingAccounts = new HashSet<Guid>();

        foreach (var subscription in dueSubscriptions)
        {
            var tenant = await _central.Tenants.FirstOrDefaultAsync(t => t.Id == subscription.TenantId, ct);
            if (tenant is null) continue;
            var plan = await _central.SubscriptionPlans.FirstAsync(p => p.Id == subscription.PlanId, ct);
            var (branches, users) = await CountTenantUsageAsync(tenant, ct);

            var isAdditionalCompany = subscription.BillingAccountId.HasValue && !seenBillingAccounts.Add(subscription.BillingAccountId.Value);
            var invoice = BuildInvoice(subscription, plan, branches, users, isAdditionalCompany);
            _central.SubscriptionInvoices.Add(invoice);
            generated++;

            subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
            subscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd.AddMonths(1);
            subscription.Status = subscription.Status == SubscriptionStatus.Trialing ? SubscriptionStatus.Active : subscription.Status;

            if (subscription.PaymentProvider != "Manual" && !string.IsNullOrEmpty(subscription.PaymentProviderTokenRef)
                && _providers.TryGetValue(subscription.PaymentProvider, out var gateway) && gateway.IsConfigured)
            {
                var result = await gateway.ChargeAsync(
                    new PaymentChargeRequest(tenant.Id, invoice.TotalAmount, invoice.Currency, subscription.PaymentProviderCustomerRef, subscription.PaymentProviderTokenRef,
                        $"RomaERP {plan.NameEn} - {tenant.CompanyNameEn} - {invoice.PeriodStart:yyyy-MM}"),
                    ct);

                if (result.Success)
                {
                    invoice.Status = SubscriptionInvoiceStatus.Paid;
                    invoice.PaidAtUtc = today;
                    invoice.PaymentReference = result.ProviderReference;
                    subscription.Status = SubscriptionStatus.Active;
                    autoCharged++;
                }
                else
                {
                    invoice.Status = SubscriptionInvoiceStatus.Failed;
                    subscription.Status = SubscriptionStatus.PastDue;
                    notes.Add($"{tenant.CompanyNameEn}: فشل التحصيل التلقائي — {result.FailureReason}");
                }
            }
        }

        await _central.SaveChangesAsync(ct);

        var suspended = await SuspendOverdueAsync(today, ct);

        return new BillingRunResultDto(generated, autoCharged, suspended, notes);
    }

    private async Task<int> SuspendOverdueAsync(DateTime today, CancellationToken ct)
    {
        var suspendableStatuses = new[] { SubscriptionStatus.Active, SubscriptionStatus.PastDue };
        var candidates = await _central.Subscriptions.Where(s => suspendableStatuses.Contains(s.Status)).ToListAsync(ct);
        var suspendedCount = 0;

        foreach (var subscription in candidates)
        {
            var oldestUnpaid = await _central.SubscriptionInvoices
                .Where(i => i.SubscriptionId == subscription.Id
                            && (i.Status == SubscriptionInvoiceStatus.Pending || i.Status == SubscriptionInvoiceStatus.Failed))
                .OrderBy(i => i.DueDateUtc)
                .FirstOrDefaultAsync(ct);

            if (oldestUnpaid is null) continue;
            if (today <= oldestUnpaid.DueDateUtc.AddDays(SubscriptionPricingConstants.GracePeriodDays)) continue;

            subscription.Status = SubscriptionStatus.Suspended;
            subscription.SuspendedAtUtc = today;
            var tenant = await _central.Tenants.FirstOrDefaultAsync(t => t.Id == subscription.TenantId, ct);
            if (tenant is not null) tenant.IsActive = false;
            suspendedCount++;
        }

        if (suspendedCount > 0) await _central.SaveChangesAsync(ct);
        return suspendedCount;
    }

    private static SubscriptionInvoice BuildInvoice(Subscription subscription, SubscriptionPlan plan, int branches, int users, bool isAdditionalCompany)
    {
        var pricing = SubscriptionInvoiceCalculator.Compute(
            plan.MonthlyBasePrice, plan.IncludedBranches, plan.IncludedUsers, plan.IsCustomPricing, branches, users, isAdditionalCompany);

        return new SubscriptionInvoice
        {
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            PlanCode = plan.Code,
            PlanNameAr = plan.NameAr,
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd,
            BaseAmount = pricing.BaseAmount,
            ExtraBranches = pricing.ExtraBranches,
            ExtraBranchesAmount = pricing.ExtraBranchesAmount,
            ExtraUsers = pricing.ExtraUsers,
            ExtraUsersAmount = pricing.ExtraUsersAmount,
            MultiCompanyDiscountAmount = pricing.MultiCompanyDiscountAmount,
            TotalAmount = pricing.TotalAmount,
            Currency = SubscriptionPricingConstants.DefaultCurrency,
            Status = SubscriptionInvoiceStatus.Pending,
            DueDateUtc = subscription.CurrentPeriodEnd,
        };
    }

    private async Task<Subscription> GetOrCreateSubscriptionAsync(Tenant tenant, CancellationToken ct)
    {
        var existing = await _central.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenant.Id, ct);
        if (existing is not null) return existing;

        var defaultPlan = await _central.SubscriptionPlans.OrderBy(p => p.SortOrder).FirstAsync(ct);
        var now = DateTime.UtcNow;

        var subscription = new Subscription
        {
            TenantId = tenant.Id,
            PlanId = defaultPlan.Id,
            Status = tenant.IsDemo ? SubscriptionStatus.Trialing : SubscriptionStatus.Active,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = tenant.IsDemo && tenant.ExpiresAtUtc.HasValue ? tenant.ExpiresAtUtc.Value : now.AddMonths(1),
            PaymentProvider = "Manual",
        };
        _central.Subscriptions.Add(subscription);
        await _central.SaveChangesAsync(ct);
        return subscription;
    }

    private async Task<Tenant> GetTenantOrThrowAsync(Guid tenantId, CancellationToken ct)
        => await _central.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
           ?? throw new NotFoundException(nameof(Tenant), tenantId);

    private async Task<TenantSubscriptionDto> BuildDtoAsync(Tenant tenant, Subscription subscription, SubscriptionPlan plan, CancellationToken ct)
    {
        var (branches, users) = await CountTenantUsageAsync(tenant, ct);
        var outstanding = await _central.SubscriptionInvoices.AsNoTracking()
            .Where(i => i.TenantId == tenant.Id && i.Status != SubscriptionInvoiceStatus.Paid && i.Status != SubscriptionInvoiceStatus.Cancelled)
            .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0;
        return MapTenantSubscription(tenant, subscription, plan, branches, users, outstanding);
    }

    /// <summary>Opens a fresh scope resolved to this tenant's own database, purely to count active
    /// branches/users for overage billing — the same numbers the in-app Usage indicator shows.</summary>
    private async Task<(int Branches, int Users)> CountTenantUsageAsync(Tenant tenant, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(tenant, _registry.BuildConnectionString(tenant.DatabaseName));

        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var branches = await db.Warehouses.CountAsync(w => w.IsActive, ct);
        var users = await userManager.Users.CountAsync(u => u.IsActive, ct);
        return (branches, users);
    }

    private static SubscriptionPlanDto MapPlan(SubscriptionPlan p) =>
        new(p.Id, p.Code, p.NameAr, p.NameEn, p.MonthlyBasePrice, p.IncludedBranches, p.IncludedUsers, p.IsCustomPricing, p.IsActive);

    private static TenantSubscriptionDto MapTenantSubscription(Tenant tenant, Subscription s, SubscriptionPlan plan, int branches, int users, decimal outstanding) =>
        new(tenant.Id, tenant.CompanyCode, tenant.CompanyNameAr, tenant.CompanyNameEn, tenant.IsActive,
            s.Id, plan.Id, plan.Code, plan.NameAr, s.Status, s.CurrentPeriodStart, s.CurrentPeriodEnd,
            s.BillingAccountId, s.PaymentProvider, branches, users, outstanding);

    private static SubscriptionInvoiceDto MapInvoice(SubscriptionInvoice i, string companyNameAr) =>
        new(i.Id, i.TenantId, companyNameAr, i.PlanCode, i.PlanNameAr, i.PeriodStart, i.PeriodEnd,
            i.BaseAmount, i.ExtraBranches, i.ExtraBranchesAmount, i.ExtraUsers, i.ExtraUsersAmount,
            i.MultiCompanyDiscountAmount, i.TotalAmount, i.Currency, i.Status, i.DueDateUtc, i.PaidAtUtc, i.PaymentReference);
}
