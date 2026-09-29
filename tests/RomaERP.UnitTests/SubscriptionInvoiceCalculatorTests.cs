using RomaERP.Application.Common;
using Xunit;

namespace RomaERP.UnitTests;

public class SubscriptionInvoiceCalculatorTests
{
    [Fact]
    public void Compute_WithinIncludedLimits_ChargesOnlyBasePrice()
    {
        var result = SubscriptionInvoiceCalculator.Compute(
            monthlyBasePrice: 799m, includedBranches: 3, includedUsers: 5, isCustomPricing: false,
            actualBranches: 3, actualUsers: 5, isAdditionalCompany: false);

        Assert.Equal(0, result.ExtraBranches);
        Assert.Equal(0, result.ExtraUsers);
        Assert.Equal(0m, result.ExtraBranchesAmount);
        Assert.Equal(0m, result.ExtraUsersAmount);
        Assert.Equal(0m, result.MultiCompanyDiscountAmount);
        Assert.Equal(799m, result.TotalAmount);
    }

    [Fact]
    public void Compute_BeyondIncludedLimits_ChargesOverageAtPublishedRates()
    {
        // Business tier (SAR list): 7 branches / 25 users included; here we're 2 branches and 4 users over.
        var result = SubscriptionInvoiceCalculator.Compute(
            monthlyBasePrice: 349m, includedBranches: 7, includedUsers: 25, isCustomPricing: false,
            actualBranches: 9, actualUsers: 29, isAdditionalCompany: false);

        Assert.Equal(2, result.ExtraBranches);
        Assert.Equal(30m, result.ExtraBranchesAmount); // 2 * 15 SAR
        Assert.Equal(4, result.ExtraUsers);
        Assert.Equal(80m, result.ExtraUsersAmount); // 4 * 20 SAR
        Assert.Equal(0m, result.MultiCompanyDiscountAmount);
        Assert.Equal(349m + 30m + 80m, result.TotalAmount);
    }

    [Fact]
    public void Compute_WithACurrencysOwnOveragePrices_UsesThemInsteadOfTheSarDefaults()
    {
        // Egypt's independent list: 200 EGP per extra branch, 400 EGP per extra user.
        var result = SubscriptionInvoiceCalculator.Compute(
            monthlyBasePrice: 7000m, includedBranches: 7, includedUsers: 25, isCustomPricing: false,
            actualBranches: 8, actualUsers: 26, isAdditionalCompany: false,
            extraBranchPrice: 200m, extraUserPrice: 400m);

        Assert.Equal(200m, result.ExtraBranchesAmount);
        Assert.Equal(400m, result.ExtraUsersAmount);
        Assert.Equal(7600m, result.TotalAmount);
    }

    [Fact]
    public void Compute_AdditionalCompanyInSameBillingAccount_Gets15PercentDiscount()
    {
        var result = SubscriptionInvoiceCalculator.Compute(
            monthlyBasePrice: 499m, includedBranches: 1, includedUsers: 2, isCustomPricing: false,
            actualBranches: 1, actualUsers: 2, isAdditionalCompany: true);

        Assert.Equal(499m * 0.15m, result.MultiCompanyDiscountAmount);
        Assert.Equal(499m * 0.85m, result.TotalAmount);
    }

    [Fact]
    public void Compute_CustomPricingPlan_NeverChargesOverage()
    {
        // Enterprise tier: negotiated price, unlimited usage — overage must never auto-apply.
        var result = SubscriptionInvoiceCalculator.Compute(
            monthlyBasePrice: 2499m, includedBranches: int.MaxValue, includedUsers: int.MaxValue, isCustomPricing: true,
            actualBranches: 40, actualUsers: 120, isAdditionalCompany: false);

        Assert.Equal(0, result.ExtraBranches);
        Assert.Equal(0, result.ExtraUsers);
        Assert.Equal(2499m, result.TotalAmount);
    }

    [Fact]
    public void Compute_UsageBelowIncluded_NeverGoesNegative()
    {
        var result = SubscriptionInvoiceCalculator.Compute(
            monthlyBasePrice: 499m, includedBranches: 3, includedUsers: 5, isCustomPricing: false,
            actualBranches: 1, actualUsers: 1, isAdditionalCompany: false);

        Assert.Equal(0, result.ExtraBranches);
        Assert.Equal(0, result.ExtraUsers);
        Assert.Equal(499m, result.TotalAmount);
    }
}
