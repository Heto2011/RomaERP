namespace RomaERP.Application.Restaurant;

public static class RestaurantConstants
{
    /// <summary>Every dine-in/takeaway/delivery order bills under this shared Customer record — a restaurant
    /// walk-in has no real ongoing AR relationship, so there is no per-customer Customer entity to bill to.</summary>
    public const string WalkInCustomerCode = "WALK-IN";
    public const string WalkInCustomerNameAr = "عميل مطعم (نقدي)";
    public const string WalkInCustomerNameEn = "Restaurant Walk-in Customer";

    public const string RestaurantOrderReference = "RESTAURANT-ORDER";
}
