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
        var card = await CardPlanAsync(ct);
        if (card is null || _tenant.ProductScope == ProductScope.PeopleOnly) return;
        var (plan, sub) = card.Value;
        if (plan.IncludedBranches == int.MaxValue) return;

        var allowed = plan.IncludedBranches + sub.ExtraBranchesPaid;
        var current = await _db.Warehouses.CountAsync(w => w.IsActive, ct);
        if (current >= allowed)
            throw new ValidationAppException(
                $"باقتك ({plan.NameAr}) بتشمل {plan.IncludedBranches} فروع{(sub.ExtraBranchesPaid > 0 ? $" + {sub.ExtraBranchesPaid} إضافي" : "")}، وده العدد الحالي. من «اشتراكي» ← «الإضافات» تقدر تشتري فرع زيادة، أو ترقّي الباقة.");
    }

    public async Task EnsureCanAddUserAsync(CancellationToken ct = default)
    {
        var card = await CardPlanAsync(ct);
        if (card is null || _tenant.ProductScope == ProductScope.PeopleOnly) return; // Roma HR counts employees, not logins
        var (plan, sub) = card.Value;
        if (plan.IncludedUsers == int.MaxValue) return;

        var allowed = plan.IncludedUsers + sub.ExtraUsersPaid;
        var current = await _users.Users.CountAsync(u => u.IsActive, ct);
        if (current >= allowed)
            throw new ValidationAppException(
                $"باقتك ({plan.NameAr}) بتشمل {plan.IncludedUsers} مستخدمين{(sub.ExtraUsersPaid > 0 ? $" + {sub.ExtraUsersPaid} إضافي" : "")}، وده العدد الحالي. من «اشتراكي» ← «الإضافات» تقدر تشتري مستخدم زيادة، أو ترقّي الباقة.");
    }

    public async Task EnsureCanAddEmployeeAsync(CancellationToken ct = default)
    {
        var card = await CardPlanAsync(ct);
        if (card is null || _tenant.ProductScope != ProductScope.PeopleOnly) return;
        var (plan, sub) = card.Value;
        if (plan.IncludedUsers == int.MaxValue) return;

        var allowed = plan.IncludedUsers + sub.ExtraUsersPaid;
        var current = await ActiveEmployeesAsync(ct);
        if (current >= allowed)
            throw new ValidationAppException(
                $"باقتك بتشمل {plan.IncludedUsers} موظف{(sub.ExtraUsersPaid > 0 ? $" + {sub.ExtraUsersPaid} إضافي" : "")}، وده العدد الحالي. من «اشتراكي» ← «الإضافات» تقدر تشتري موظفين زيادة.");
    }

    public async Task<PlanUsageDto?> GetUsageAsync(CancellationToken ct = default)
    {
        var card = await CardPlanAsync(ct);
        if (card is null) return null;
        var (plan, sub) = card.Value;
        var isHr = _tenant.ProductScope == ProductScope.PeopleOnly;

        var branches = isHr ? 0 : await _db.Warehouses.CountAsync(w => w.IsActive, ct);
        var users = isHr ? await ActiveEmployeesAsync(ct) : await _users.Users.CountAsync(u => u.IsActive, ct);
        return new PlanUsageDto(isHr, plan.Code, plan.NameEn,
            plan.IncludedBranches, sub.ExtraBranchesPaid, branches,
            plan.IncludedUsers, sub.ExtraUsersPaid, users);
    }

    private Task<int> ActiveEmployeesAsync(CancellationToken ct)
        => _db.Employees.CountAsync(e => !e.IsDeleted && e.EmploymentStatus == RomaERP.Domain.HR.EmploymentStatus.Active, ct);

    /// <summary>The plan and subscription to enforce, or null when this company must not be limited (not card-paid, or a negotiated plan).</summary>
    private async Task<(SubscriptionPlan Plan, Subscription Sub)?> CardPlanAsync(CancellationToken ct)
    {
        if (!_tenant.IsResolved) return null;

        var subscription = await _central.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == _tenant.TenantId, ct);
        if (subscription is null || subscription.PaymentProvider != LemonSqueezyService.ProviderName) return null;

        var plan = await _central.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == subscription.PlanId, ct);
        return plan is null || plan.IsCustomPricing ? null : (plan, subscription);
    }
}
