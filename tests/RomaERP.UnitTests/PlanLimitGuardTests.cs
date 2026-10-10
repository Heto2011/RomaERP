using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Billing;
using RomaERP.Infrastructure.Persistence;
using RomaERP.Infrastructure.Persistence.Central;
using Xunit;

namespace RomaERP.UnitTests;

public class PlanLimitGuardTests
{
    private class FakeTenant : ITenantContext
    {
        public Guid TenantId { get; init; } = Guid.NewGuid();
        public string CompanyCode => "t";
        public string ConnectionString => "";
        public Country Country => Country.SaudiArabia;
        public ProductScope ProductScope { get; init; } = ProductScope.Full;
        public bool IsResolved => true;
    }

    private static (PlanLimitGuard Guard, ApplicationDbContext Db) Build(string provider, int includedBranches, bool custom = false, ProductScope scope = ProductScope.Full)
    {
        var central = new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = new FakeTenant { ProductScope = scope };
        var plan = new SubscriptionPlan { Code = "essential", NameAr = "الأساسية", NameEn = "Essential", MonthlyBasePrice = 149, IncludedBranches = includedBranches, IncludedUsers = 10, IsCustomPricing = custom };
        central.SubscriptionPlans.Add(plan);
        central.Subscriptions.Add(new Subscription { TenantId = tenant.TenantId, PlanId = plan.Id, Status = SubscriptionStatus.Active, PaymentProvider = provider });
        central.SaveChanges();
        return (new PlanLimitGuard(central, tenant, db, null!), db);
    }

    private static void AddBranches(ApplicationDbContext db, int count)
    {
        for (var i = 0; i < count; i++)
            db.Warehouses.Add(new Warehouse { Code = "W" + i, NameAr = "ف" + i, NameEn = "B" + i, IsActive = true });
        db.SaveChanges();
    }

    [Fact]
    public async Task CardPaidCompany_IsBlockedAtItsPlanBranchLimit()
    {
        var (guard, db) = Build("LemonSqueezy", includedBranches: 3);
        AddBranches(db, 2);
        await guard.EnsureCanAddBranchAsync(); // the 3rd is still inside the plan

        AddBranches(db, 1);
        await Assert.ThrowsAsync<ValidationAppException>(() => guard.EnsureCanAddBranchAsync()); // a 4th is not
    }

    [Theory]
    [InlineData("Manual")]
    public async Task InvoicePaidCompany_IsNeverBlocked_ItsExtrasAreBilledInstead(string provider)
    {
        var (guard, db) = Build(provider, includedBranches: 1);
        AddBranches(db, 5);

        await guard.EnsureCanAddBranchAsync();
    }

    [Fact]
    public async Task NegotiatedPlanAndRomaHr_AreNeverBlocked()
    {
        var (custom, db1) = Build("LemonSqueezy", includedBranches: 1, custom: true);
        AddBranches(db1, 5);
        await custom.EnsureCanAddBranchAsync();

        var (hr, db2) = Build("LemonSqueezy", includedBranches: 1, scope: ProductScope.PeopleOnly);
        AddBranches(db2, 5);
        await hr.EnsureCanAddBranchAsync();
    }
}
