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

    private bool IsFreeBooks => _tenant.IsResolved && _tenant.ProductScope == ProductScope.BooksOnly;

    private enum FreeTier { None, Books, HrFree, Pos }

    /// <summary>Which free plan (if any) this company is on. Books is decided by its product; free HR and POS by their plan code,
    /// so a company that paid for the HR plan is never treated as free.</summary>
    private async Task<FreeTier> FreeTierAsync(CancellationToken ct)
    {
        if (!_tenant.IsResolved) return FreeTier.None;
        if (_tenant.ProductScope == ProductScope.BooksOnly) return FreeTier.Books;
        if (_tenant.ProductScope is not (ProductScope.PeopleOnly or ProductScope.PosOnly)) return FreeTier.None;
        var code = await _central.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == _tenant.TenantId)
            .Join(_central.SubscriptionPlans, s => s.PlanId, p => p.Id, (s, p) => p.Code)
            .FirstOrDefaultAsync(ct);
        return code == SubscriptionPriceList.HrFreePlanCode ? FreeTier.HrFree
             : code == SubscriptionPriceList.PosPlanCode ? FreeTier.Pos
             : FreeTier.None;
    }

    private static string TierName(FreeTier t) => t switch { FreeTier.HrFree => "Roma HR", FreeTier.Pos => "Roma POS", _ => "Roma Books" };

    public async Task EnsureCanAddBranchAsync(CancellationToken ct = default)
    {
        var tier = await FreeTierAsync(ct);
        if (tier is FreeTier.Books or FreeTier.Pos)
        {
            var allowedBranches = tier == FreeTier.Pos ? SubscriptionPriceList.PosPlanIncludedBranches : SubscriptionPriceList.BooksIncludedBranches;
            if (await _db.Warehouses.CountAsync(w => w.IsActive, ct) >= allowedBranches)
                throw new ValidationAppException($"النسخة المجانية ({TierName(tier)}) بتشمل {allowedBranches} فرع. للمزيد رقّي الباقة من «اشتراكي».");
            return;
        }
        var card = await CardPlanAsync(ct);
        if (card is null)
        {
            await EnsureWithinEgyptCapAsync(branches: true, ct);
            return;
        }
        if (_tenant.ProductScope == ProductScope.PeopleOnly) return;
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
        var tier = await FreeTierAsync(ct);
        if (tier != FreeTier.None)
        {
            var allowedUsers = tier switch
            {
                FreeTier.HrFree => SubscriptionPriceList.HrFreeIncludedUsers,
                FreeTier.Pos => SubscriptionPriceList.PosPlanIncludedUsers,
                _ => SubscriptionPriceList.BooksIncludedUsers
            };
            if (await _users.Users.CountAsync(u => u.IsActive, ct) >= allowedUsers)
                throw new ValidationAppException($"النسخة المجانية ({TierName(tier)}) بتشمل {allowedUsers} مستخدم. للمزيد رقّي الباقة من «اشتراكي».");
            return;
        }
        var card = await CardPlanAsync(ct);
        if (card is null)
        {
            await EnsureWithinEgyptCapAsync(branches: false, ct);
            return;
        }
        if (_tenant.ProductScope == ProductScope.PeopleOnly) return; // Roma HR counts employees, not logins
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
        if (await FreeTierAsync(ct) == FreeTier.HrFree)
        {
            if (await ActiveEmployeesAsync(ct) >= SubscriptionPriceList.HrFreeIncludedEmployees)
                throw new ValidationAppException($"النسخة المجانية (Roma HR) بتشمل {SubscriptionPriceList.HrFreeIncludedEmployees} موظفين. للمزيد رقّي لباقة HR المدفوعة من «اشتراكي».");
            return;
        }
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

    /// <summary>Egypt (invoice-billed) companies on Mini or Essential have a hard ceiling: Mini none beyond what it includes,
    /// Essential +1 branch and +4 users (see <see cref="SubscriptionPriceList.EgyptExtrasCap"/>). Beyond that they move up a plan.</summary>
    private async Task EnsureWithinEgyptCapAsync(bool branches, CancellationToken ct)
    {
        if (!_tenant.IsResolved || _tenant.Country != Country.Egypt || _tenant.ProductScope == ProductScope.PeopleOnly) return;

        var plan = await (from s in _central.Subscriptions.AsNoTracking()
                          join p in _central.SubscriptionPlans.AsNoTracking() on s.PlanId equals p.Id
                          where s.TenantId == _tenant.TenantId
                          select p).FirstOrDefaultAsync(ct);
        if (plan is null || plan.IsCustomPricing) return;
        if (SubscriptionPriceList.EgyptExtrasCap(plan.Code, plan.IncludedBranches, plan.IncludedUsers) is not { } cap) return;

        var included = branches ? plan.IncludedBranches : plan.IncludedUsers;
        var allowed = included + (branches ? cap.Branches : cap.Users);
        var current = branches ? await _db.Warehouses.CountAsync(w => w.IsActive, ct) : await _users.Users.CountAsync(u => u.IsActive, ct);
        if (current < allowed) return;

        var what = branches ? "فروع" : "مستخدمين";
        var extras = branches ? cap.Branches : cap.Users;
        throw new ValidationAppException(extras == 0
            ? $"باقتك ({plan.NameAr}) بتشمل {included} {what} ومفيهاش إضافات. لزيادة {what} رقّي الباقة للأساسية — كلّمنا من «الدعم»."
            : $"باقتك ({plan.NameAr}) بتشمل {included} {what} + {extras} إضافي كحد أقصى، وده العدد الحالي. لزيادة {what} أكتر رقّي الباقة — كلّمنا من «الدعم».");
    }

    public async Task EnsureCanCreateInvoiceAsync(CancellationToken ct = default)
    {
        if (!IsFreeBooks || SubscriptionPriceList.BooksMonthlyInvoiceLimit <= 0) return;
        var used = await InvoicesThisMonthAsync(ct);
        if (used >= SubscriptionPriceList.BooksMonthlyInvoiceLimit)
            throw new ValidationAppException($"وصلت للحد الشهري في النسخة المجانية (Roma Books): {SubscriptionPriceList.BooksMonthlyInvoiceLimit} فاتورة (مبيعات ومشتريات). يتجدد أول الشهر الجاي، أو رقّي الباقة من «اشتراكي».");
    }

    private async Task<int> InvoicesThisMonthAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        return await _db.SalesInvoices.CountAsync(i => i.CreatedAtUtc >= monthStart, ct)
             + await _db.PurchaseInvoices.CountAsync(i => i.CreatedAtUtc >= monthStart, ct);
    }

    public async Task<PlanUsageDto?> GetUsageAsync(CancellationToken ct = default)
    {
        var tier = await FreeTierAsync(ct);
        if (tier == FreeTier.HrFree)
            return new PlanUsageDto(true, SubscriptionPriceList.HrFreePlanCode, "Roma HR (free)",
                0, 0, 0, SubscriptionPriceList.HrFreeIncludedUsers, 0, await _users.Users.CountAsync(u => u.IsActive, ct),
                true, 0, 0, SubscriptionPriceList.HrFreeIncludedEmployees, await ActiveEmployeesAsync(ct));
        if (tier == FreeTier.Pos)
            return new PlanUsageDto(false, SubscriptionPriceList.PosPlanCode, "Roma POS (free)",
                SubscriptionPriceList.PosPlanIncludedBranches, 0, await _db.Warehouses.CountAsync(w => w.IsActive, ct),
                SubscriptionPriceList.PosPlanIncludedUsers, 0, await _users.Users.CountAsync(u => u.IsActive, ct), true);
        if (tier == FreeTier.Books)
            return new PlanUsageDto(false, SubscriptionPriceList.BooksPlanCode, "Roma Books",
                SubscriptionPriceList.BooksIncludedBranches, 0, await _db.Warehouses.CountAsync(w => w.IsActive, ct),
                SubscriptionPriceList.BooksIncludedUsers, 0, await _users.Users.CountAsync(u => u.IsActive, ct),
                true, SubscriptionPriceList.BooksMonthlyInvoiceLimit, await InvoicesThisMonthAsync(ct));

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
