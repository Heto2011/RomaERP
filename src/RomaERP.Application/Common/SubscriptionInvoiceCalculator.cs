using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Common;

/// <summary>Pure pricing math for one subscription invoice — mirrors the public calculator on
/// marketing/pricing.html exactly, so both sides always agree on what a customer will actually be charged.</summary>
public static class SubscriptionInvoiceCalculator
{
    public record Result(
        decimal BaseAmount,
        int ExtraBranches,
        decimal ExtraBranchesAmount,
        int ExtraUsers,
        decimal ExtraUsersAmount,
        decimal MultiCompanyDiscountAmount,
        decimal TotalAmount);

    public static Result Compute(
        decimal monthlyBasePrice,
        int includedBranches,
        int includedUsers,
        bool isCustomPricing,
        int actualBranches,
        int actualUsers,
        bool isAdditionalCompany)
    {
        var extraBranches = isCustomPricing ? 0 : Math.Max(0, actualBranches - includedBranches);
        var extraUsers = isCustomPricing ? 0 : Math.Max(0, actualUsers - includedUsers);
        var extraBranchesAmount = extraBranches * SubscriptionPricingConstants.ExtraBranchPrice;
        var extraUsersAmount = extraUsers * SubscriptionPricingConstants.ExtraUserPrice;
        var subtotal = monthlyBasePrice + extraBranchesAmount + extraUsersAmount;
        var discount = isAdditionalCompany ? subtotal * (1 - SubscriptionPricingConstants.AdditionalCompanyDiscountMultiplier) : 0;

        return new Result(monthlyBasePrice, extraBranches, extraBranchesAmount, extraUsers, extraUsersAmount, discount, subtotal - discount);
    }
}
