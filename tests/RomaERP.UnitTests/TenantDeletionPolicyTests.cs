using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Tenancy;
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
}
