using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Identity;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.UnitTests;

public class TenantDeletionPolicyTests
{
    [Fact]
    public void A_company_created_before_the_cutoff_is_a_test_company_and_can_be_erased()
    {
        var tenant = new Tenant { CreatedAtUtc = TenantProvisioningService.LegacyTestCleanupCutoffUtc.AddDays(-30) };

        TenantProvisioningService.EnsureLegacyTestCompany(tenant);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(400)]
    public void A_company_created_at_or_after_the_cutoff_is_a_real_customer_and_is_refused(int daysAfter)
    {
        var tenant = new Tenant { CreatedAtUtc = TenantProvisioningService.LegacyTestCleanupCutoffUtc.AddDays(daysAfter) };

        var ex = Assert.Throws<ValidationAppException>(() => TenantProvisioningService.EnsureLegacyTestCompany(tenant));
        Assert.Contains("support@romagroup.app", ex.Message);
    }

    [Fact]
    public void An_active_admin_of_the_company_is_an_official_requester()
    {
        TenantProvisioningService.EnsureOfficialRequester(new ApplicationUser { IsActive = true }, isAdmin: true);
    }

    [Fact]
    public void An_unknown_email_is_refused()
    {
        Assert.Throws<ValidationAppException>(() => TenantProvisioningService.EnsureOfficialRequester(null, isAdmin: false));
    }

    [Fact]
    public void A_user_who_is_not_an_admin_is_refused()
    {
        Assert.Throws<ValidationAppException>(() => TenantProvisioningService.EnsureOfficialRequester(new ApplicationUser { IsActive = true }, isAdmin: false));
    }

    [Fact]
    public void A_deactivated_admin_is_refused()
    {
        Assert.Throws<ValidationAppException>(() => TenantProvisioningService.EnsureOfficialRequester(new ApplicationUser { IsActive = false }, isAdmin: true));
    }
}
