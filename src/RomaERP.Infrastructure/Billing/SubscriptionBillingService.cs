using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public SubscriptionBillingService(
        CentralDbContext central,
        IServiceScopeFactory scopeFactory,
        ITenantRegistry registry,
        IEnumerable<IPaymentGatewayProvider> providers,
        IConfiguration configuration)
    {
        _central = central;
        _scopeFactory = scopeFactory;
        _registry = registry;
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _configuration = configuration;
    }

    /// <summary>Off by default: an unpaid invoice only shows up as "overdue" for the owner to act on. Turning this on
    /// makes the billing run suspend a tenant automatically once an invoice is past the grace period.</summary>
    private bool AutoSuspendOverdue => _configuration.GetValue<bool>("Billing:AutoSuspendOverdue");

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

            result.Add(MapTenantSubscription(tenant, subscription, plan, branches, users, outstanding, await GetOverdueDaysAsync(tenant.Id, ct)));
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

    public async Task<TenantSubscriptionDto> SetBillingPeriodAsync(Guid tenantId, BillingPeriod billingPeriod, CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);
        var subscription = await GetOrCreateSubscriptionAsync(tenant, ct);
        subscription.BillingPeriod = billingPeriod;
        await _central.SaveChangesAsync(ct);

        var plan = await _central.SubscriptionPlans.AsNoTracking().FirstAsync(p => p.Id == subscription.PlanId, ct);
        return await BuildDtoAsync(tenant, subscription, plan, ct);
    }

    public async Task<TenantSubscriptionDto> ActivatePaidAsync(Guid tenantId, Guid planId, string? paymentReference, BillingPeriod billingPeriod = BillingPeriod.Monthly, CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);
        var plan = await _central.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), planId);
        var subscription = await GetOrCreateSubscriptionAsync(tenant, ct);
        var now = DateTime.UtcNow;

        // First confirmed payment turns a trial into a customer for good. Clearing the trial flags matters: the
        // expiry sweep locks any tenant that is still IsDemo past ExpiresAtUtc, even one the owner just reactivated.
        tenant.IsDemo = false;
        tenant.ExpiresAtUtc = null;
        tenant.IsActive = true;

        subscription.PlanId = plan.Id;
        subscription.Status = SubscriptionStatus.Active;
        subscription.SuspendedAtUtc = null;
        subscription.BillingPeriod = billingPeriod;
        subscription.CurrentPeriodStart = now;
        subscription.CurrentPeriodEnd = now.AddMonths(MonthsCovered(billingPeriod));

        var (branches, users) = await CountTenantUsageAsync(tenant, ct);
        var priceSet = await ResolvePriceSetAsync(subscription, tenant, plan, ct);
        var isAdditionalCompany = subscription.BillingAccountId.HasValue && await _central.Subscriptions.AnyAsync(
            s => s.BillingAccountId == subscription.BillingAccountId && s.Id != subscription.Id && s.Status == SubscriptionStatus.Active, ct);

        var invoice = BuildInvoice(subscription, plan, priceSet, branches, users, isAdditionalCompany);
        invoice.Status = SubscriptionInvoiceStatus.Paid;
        invoice.PaidAtUtc = now;
        invoice.DueDateUtc = now;
        invoice.PaymentReference = paymentReference;
        _central.SubscriptionInvoices.Add(invoice);

        await _central.SaveChangesAsync(ct);
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
            // Trialing is deliberately left out: an expired trial is not a customer. It becomes one only when the
            // owner confirms the first payment (ActivatePaidAsync); invoicing it automatically would bill everyone who just tried.
            .Where(s => (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue)
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
            var priceSet = await ResolvePriceSetAsync(subscription, tenant, plan, ct);
            var invoice = BuildInvoice(subscription, plan, priceSet, branches, users, isAdditionalCompany);
            _central.SubscriptionInvoices.Add(invoice);
            // Saved per invoice so the founding-customer ranking above sees invoices generated earlier in this same run.
            await _central.SaveChangesAsync(ct);
            generated++;

            subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
            subscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd.AddMonths(MonthsCovered(subscription.BillingPeriod));
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

        var suspended = 0;
        if (AutoSuspendOverdue)
            suspended = await SuspendOverdueAsync(today, ct);
        else
            await AddOverdueNotesAsync(today, notes, ct);

        return new BillingRunResultDto(generated, autoCharged, suspended, notes);
    }

    /// <summary>The alert-only path: lists every tenant whose oldest unpaid invoice is past the grace period, without
    /// touching the tenant, so the owner decides.</summary>
    private async Task AddOverdueNotesAsync(DateTime today, List<string> notes, CancellationToken ct)
    {
        var late = await _central.SubscriptionInvoices.AsNoTracking()
            .Where(i => (i.Status == SubscriptionInvoiceStatus.Pending || i.Status == SubscriptionInvoiceStatus.Failed)
                        && i.DueDateUtc < today.AddDays(-SubscriptionPricingConstants.GracePeriodDays))
            .GroupBy(i => i.TenantId)
            .Select(g => new { TenantId = g.Key, Oldest = g.Min(i => i.DueDateUtc) })
            .ToListAsync(ct);
        if (late.Count == 0) return;

        var names = await _central.Tenants.AsNoTracking().Where(t => late.Select(l => l.TenantId).Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.CompanyNameEn, ct);
        foreach (var l in late)
            notes.Add($"{names.GetValueOrDefault(l.TenantId, l.TenantId.ToString())}: متأخر عن الدفع {(int)(today - l.Oldest).TotalDays} يوم — راجع وأوقف الحساب يدويًا لو لازم.");
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

    /// <summary>What this tenant is billed in: the price list for its country's currency, at the founding price
    /// while the Egypt launch offer still applies to it. Falls back to the plan row's own price (SAR) for a plan
    /// code the price list doesn't know.</summary>
    private async Task<PlanPriceSet> ResolvePriceSetAsync(Subscription subscription, Tenant tenant, SubscriptionPlan plan, CancellationToken ct)
    {
        var currency = SubscriptionPriceList.CurrencyFor(tenant.Country);
        var listed = SubscriptionPriceList.Find(plan.Code, currency);
        if (listed is null)
            return new PlanPriceSet(SubscriptionPriceList.Sar, plan.MonthlyBasePrice, 0,
                SubscriptionPricingConstants.ExtraBranchPrice, SubscriptionPricingConstants.ExtraUserPrice);

        // The launch offer is a monthly-billing offer; an annual subscriber already gets two months free.
        if (listed.FoundingBase > 0 && subscription.BillingPeriod == BillingPeriod.Monthly && await IsFoundingCustomerAsync(subscription, tenant, plan, ct))
            return listed with { Base = listed.FoundingBase };

        return listed;
    }

    /// <summary>Launch offers: the first <see cref="SubscriptionPriceList.FoundingCustomerLimit"/> subscriptions to be
    /// invoiced pay the founding price for their first <see cref="SubscriptionPriceList.FoundingInvoiceCount"/> invoices.
    /// For the ERP tiers that is the Egypt offer (Egyptian tenants only); for ROMA People (HR) it is open to any country.</summary>
    private async Task<bool> IsFoundingCustomerAsync(Subscription subscription, Tenant tenant, SubscriptionPlan plan, CancellationToken ct)
    {
        IQueryable<SubscriptionInvoice> cohort;
        if (plan.Code == SubscriptionPriceList.PeoplePlanCode)
        {
            cohort = _central.SubscriptionInvoices.Where(i => i.PlanCode == SubscriptionPriceList.PeoplePlanCode);
        }
        else
        {
            if (tenant.Country != Country.Egypt) return false;
            var egyptTenantIds = _central.Tenants.Where(t => t.Country == Country.Egypt).Select(t => t.Id);
            cohort = _central.SubscriptionInvoices.Where(i => egyptTenantIds.Contains(i.TenantId));
        }

        var firstInvoices = await cohort
            .Where(i => i.Status != SubscriptionInvoiceStatus.Cancelled)
            .GroupBy(i => i.SubscriptionId)
            .Select(g => new { SubscriptionId = g.Key, First = g.Min(i => i.CreatedAtUtc), Count = g.Count() })
            .ToListAsync(ct);

        var mine = firstInvoices.FirstOrDefault(x => x.SubscriptionId == subscription.Id);
        if (mine is null) return firstInvoices.Count < SubscriptionPriceList.FoundingCustomerLimit;

        var ahead = firstInvoices.Count(x => x.First < mine.First);
        return ahead < SubscriptionPriceList.FoundingCustomerLimit && mine.Count < SubscriptionPriceList.FoundingInvoiceCount;
    }

    private static int MonthsCovered(BillingPeriod period)
        => period == BillingPeriod.Annual ? SubscriptionPriceList.AnnualMonthsCovered : 1;

    private static SubscriptionInvoice BuildInvoice(
        Subscription subscription, SubscriptionPlan plan, PlanPriceSet prices, int branches, int users, bool isAdditionalCompany)
    {
        var pricing = SubscriptionInvoiceCalculator.Compute(
            prices.Base, plan.IncludedBranches, plan.IncludedUsers, plan.IsCustomPricing, branches, users, isAdditionalCompany,
            prices.ExtraBranch, prices.ExtraUser);

        // Overage in the pegged Gulf currencies is a fraction (e.g. 14.7 AED per branch), so round every line to the
        // currency's own decimals and add the rounded lines up — the invoice always sums exactly.
        var decimals = SubscriptionPriceList.DecimalsFor(prices.Currency);
        decimal Round(decimal amount) => Math.Round(amount, decimals, MidpointRounding.AwayFromZero);
        // An annual invoice covers 12 months but is charged as 10 ("two months free"), overage included.
        var factor = subscription.BillingPeriod == BillingPeriod.Annual ? SubscriptionPriceList.AnnualMonthsCharged : 1;
        var baseAmount = Round(pricing.BaseAmount * factor);
        var extraBranchesAmount = Round(pricing.ExtraBranchesAmount * factor);
        var extraUsersAmount = Round(pricing.ExtraUsersAmount * factor);
        var discount = Round(pricing.MultiCompanyDiscountAmount * factor);

        return new SubscriptionInvoice
        {
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            PlanCode = plan.Code,
            PlanNameAr = plan.NameAr,
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd,
            BaseAmount = baseAmount,
            ExtraBranches = pricing.ExtraBranches,
            ExtraBranchesAmount = extraBranchesAmount,
            ExtraUsers = pricing.ExtraUsers,
            ExtraUsersAmount = extraUsersAmount,
            MultiCompanyDiscountAmount = discount,
            TotalAmount = baseAmount + extraBranchesAmount + extraUsersAmount - discount,
            Currency = prices.Currency,
            Status = SubscriptionInvoiceStatus.Pending,
            DueDateUtc = subscription.CurrentPeriodEnd,
        };
    }

    private async Task<Subscription> GetOrCreateSubscriptionAsync(Tenant tenant, CancellationToken ct)
    {
        var peoplePlan = tenant.ProductScope == ProductScope.PeopleOnly
            ? await _central.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == SubscriptionPriceList.PeoplePlanCode, ct)
            : null;

        var existing = await _central.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenant.Id, ct);
        if (existing is not null)
        {
            // A trial that signed up for ROMA People before it had its own plan was parked on the entry ERP tier —
            // move it, so the owner sees (and is eventually billed) the HR price, not the ERP one.
            if (peoplePlan is not null && existing.Status == SubscriptionStatus.Trialing && existing.PlanId != peoplePlan.Id)
            {
                var current = await _central.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == existing.PlanId, ct);
                if (current?.Code == "essential")
                {
                    existing.PlanId = peoplePlan.Id;
                    await _central.SaveChangesAsync(ct);
                }
            }
            return existing;
        }

        var defaultPlan = peoplePlan ?? await _central.SubscriptionPlans.OrderBy(p => p.SortOrder).FirstAsync(ct);
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
        return MapTenantSubscription(tenant, subscription, plan, branches, users, outstanding, await GetOverdueDaysAsync(tenant.Id, ct));
    }

    /// <summary>Whole days the oldest unpaid invoice is past its due date (0 when nothing is late).</summary>
    private async Task<int> GetOverdueDaysAsync(Guid tenantId, CancellationToken ct)
    {
        var oldestDue = await _central.SubscriptionInvoices.AsNoTracking()
            .Where(i => i.TenantId == tenantId && (i.Status == SubscriptionInvoiceStatus.Pending || i.Status == SubscriptionInvoiceStatus.Failed))
            .OrderBy(i => i.DueDateUtc)
            .Select(i => (DateTime?)i.DueDateUtc)
            .FirstOrDefaultAsync(ct);
        return oldestDue is { } due && due < DateTime.UtcNow ? (int)(DateTime.UtcNow - due).TotalDays : 0;
    }

    /// <summary>Opens a fresh scope resolved to this tenant's own database, purely to count active
    /// branches/users for overage billing — the same numbers the in-app Usage indicator shows.</summary>
    protected virtual async Task<(int Branches, int Users)> CountTenantUsageAsync(Tenant tenant, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(tenant, _registry.BuildConnectionString(tenant.DatabaseName));

        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var branches = await db.Warehouses.CountAsync(w => w.IsActive, ct);
        // ROMA People is priced per employee (25 included), and an employee may not have a login at all — so count the
        // active employee records there, not the login accounts, or a 59-person company with 3 logins would pay for 3.
        var users = tenant.ProductScope == ProductScope.PeopleOnly
            ? await db.Employees.CountAsync(e => !e.IsDeleted && e.EmploymentStatus == RomaERP.Domain.HR.EmploymentStatus.Active, ct)
            : await userManager.Users.CountAsync(u => u.IsActive, ct);
        return (branches, users);
    }

    private static SubscriptionPlanDto MapPlan(SubscriptionPlan p) =>
        new(p.Id, p.Code, p.NameAr, p.NameEn, p.MonthlyBasePrice, p.IncludedBranches, p.IncludedUsers, p.IsCustomPricing, p.IsActive);

    private static TenantSubscriptionDto MapTenantSubscription(Tenant tenant, Subscription s, SubscriptionPlan plan, int branches, int users, decimal outstanding, int overdueDays) =>
        new(tenant.Id, tenant.CompanyCode, tenant.CompanyNameAr, tenant.CompanyNameEn, tenant.IsActive,
            s.Id, plan.Id, plan.Code, plan.NameAr, s.Status, s.CurrentPeriodStart, s.CurrentPeriodEnd,
            s.BillingAccountId, s.PaymentProvider, branches, users, outstanding, SubscriptionPriceList.CurrencyFor(tenant.Country), overdueDays, s.BillingPeriod);

    private static SubscriptionInvoiceDto MapInvoice(SubscriptionInvoice i, string companyNameAr) =>
        new(i.Id, i.TenantId, companyNameAr, i.PlanCode, i.PlanNameAr, i.PeriodStart, i.PeriodEnd,
            i.BaseAmount, i.ExtraBranches, i.ExtraBranchesAmount, i.ExtraUsers, i.ExtraUsersAmount,
            i.MultiCompanyDiscountAmount, i.TotalAmount, i.Currency, i.Status, i.DueDateUtc, i.PaidAtUtc, i.PaymentReference);
}
