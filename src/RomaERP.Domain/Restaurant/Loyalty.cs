using RomaERP.Domain.Common;

namespace RomaERP.Domain.Restaurant;

public enum LoyaltyTransactionType
{
    /// <summary>Points added when an order is billed.</summary>
    Earn = 1,
    /// <summary>Points spent as a discount on an order.</summary>
    Redeem = 2,
    /// <summary>Manual correction by a manager (positive or negative).</summary>
    Adjust = 3,
    /// <summary>Earn/redeem undone because the billed order was voided.</summary>
    Reverse = 4
}

/// <summary>A repeat customer of the restaurant, identified by phone number. Collects points on every billed order
/// and can spend them as a discount; the linked orders form the customer's order history.</summary>
public class LoyaltyMember : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Digits only (see LoyaltyPhone.Normalize) — the unique key staff look a customer up by.</summary>
    public string Phone { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public int PointsBalance { get; set; }
    public int LifetimePointsEarned { get; set; }
    public int VisitCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastVisitUtc { get; set; }
}

/// <summary>One line of a member's points ledger — the balance on LoyaltyMember is always the sum of these.</summary>
public class LoyaltyTransaction : AuditableEntity
{
    public Guid LoyaltyMemberId { get; set; }
    public LoyaltyMember? LoyaltyMember { get; set; }

    public Guid? RestaurantOrderId { get; set; }
    public LoyaltyTransactionType Type { get; set; }
    /// <summary>Signed: positive adds to the balance, negative takes away.</summary>
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public string? Note { get; set; }
}

public static class LoyaltyPhone
{
    /// <summary>Digits only, with the common Egyptian country-code forms (+20 / 0020) folded to the local leading 0,
    /// so "+20 100 123 4567", "00201001234567" and "01001234567" all find the same member.</summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0020")) digits = "0" + digits[4..];
        else if (digits.StartsWith("20") && digits.Length == 12) digits = "0" + digits[2..];
        return digits;
    }
}
