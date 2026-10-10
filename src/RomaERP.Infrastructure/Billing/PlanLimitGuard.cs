using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Identity;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.Infrastructure.Billing;

public class PlanLimitGuard : IPlanLimitGuard
{
    private readonly CentralDbContext _central;
    private readonly ITenantContext _tenant;
    private readonly IApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public PlanLimitGuard(CentralDbContext central, ITenantContext tenant, IApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _central = central;
        _tenant = tenant;
        _db = db;
        _users = users;
    }

    public async Task EnsureCanAddBranchAsync(CancellationToken ct = default)
    {
        var plan = await CardPlanAsync(ct);
        if (plan is null || plan.IncludedBranches == int.MaxValue) return;

        var current = await _db.Warehouses.CountAsync(w => w.IsActive, ct);
        if (current >= plan.IncludedBranches)
            throw new ValidationAppException(
                $"باقتك ({plan.NameAr}) بتشمل {plan.IncludedBranches} فروع، وده العدد الحالي. عشان تضيف فرع تاني لازم ترقّي الباقة — كلّم الدعم على support@romagroup.app.");
    }

    public async Task EnsureCanAddUserAsync(CancellationToken ct = default)
    {
        var plan = await CardPlanAsync(ct);
        if (plan is null || plan.IncludedUsers == int.MaxValue) return;

        var current = await _users.Users.CountAsync(u => u.IsActive, ct);
        if (current >= plan.IncludedUsers)
            throw new ValidationAppException(
                $"باقتك ({plan.NameAr}) بتشمل {plan.IncludedUsers} مستخدمين، وده العدد الحالي. عشان تضيف مستخدم تاني لازم ترقّي الباقة — كلّم الدعم على support@romagroup.app.");
    }

    /// <summary>The plan to enforce, or null when this company must not be limited (not card-paid, HR-only, or a negotiated plan).</summary>
    private async Task<SubscriptionPlan?> CardPlanAsync(CancellationToken ct)
    {
        if (!_tenant.IsResolved || _tenant.ProductScope == ProductScope.PeopleOnly) return null;

        var subscription = await _central.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == _tenant.TenantId, ct);
        if (subscription is null || subscription.PaymentProvider != LemonSqueezyService.ProviderName) return null;

        var plan = await _central.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == subscription.PlanId, ct);
        return plan is null || plan.IsCustomPricing ? null : plan;
    }
}
