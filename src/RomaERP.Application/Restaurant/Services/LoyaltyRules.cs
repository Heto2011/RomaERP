using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.Services;

public record LoyaltySettings(bool Enabled, decimal SpendPerPoint, decimal PointValue, int MinRedeemPoints);

/// <summary>Pure points arithmetic, kept apart from the database so it can be tested on its own.</summary>
public static class LoyaltyRules
{
    /// <summary>Whole points earned for a net amount (after discounts, before VAT): one per <c>SpendPerPoint</c> spent, rounded down.</summary>
    public static int PointsEarned(decimal netAmount, LoyaltySettings s)
        => s.Enabled && s.SpendPerPoint > 0 && netAmount > 0 ? (int)Math.Floor(netAmount / s.SpendPerPoint) : 0;

    /// <summary>The points an order will actually spend and the discount they give. The request is capped so the discount never
    /// exceeds what is left to pay (<paramref name="payableBeforeLoyalty"/>) and points are not taken for discount that can't be used.</summary>
    public static (int Points, decimal Discount) Redemption(RestaurantOrder order, decimal payableBeforeLoyalty, LoyaltySettings s)
    {
        if (!s.Enabled || order.LoyaltyMemberId is null || order.LoyaltyPointsToRedeem <= 0 || s.PointValue <= 0)
            return (0, 0m);

        var maxDiscount = Math.Max(0m, payableBeforeLoyalty);
        var discount = Math.Min(Math.Round(order.LoyaltyPointsToRedeem * s.PointValue, 2), maxDiscount);
        if (discount <= 0) return (0, 0m);

        var points = (int)Math.Min(order.LoyaltyPointsToRedeem, Math.Ceiling(discount / s.PointValue));
        return (points, discount);
    }
}
