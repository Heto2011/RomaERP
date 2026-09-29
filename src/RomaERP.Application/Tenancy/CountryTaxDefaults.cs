using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Tenancy;

public record CountryTaxDefault(string TaxNameAr, decimal VatRate, string Currency);

/// <summary>Default VAT rate and currency per country, used to seed a new tenant's CompanySettings.
/// Covers Egypt, the GCC, the UK and Guernsey; extend this switch as more countries onboard.</summary>
public static class CountryTaxDefaults
{
    public static CountryTaxDefault Get(Country country) => country switch
    {
        Country.Egypt => new("ضريبة القيمة المضافة", 0.14m, "EGP"),
        Country.SaudiArabia => new("ضريبة القيمة المضافة", 0.15m, "SAR"),
        Country.UAE => new("ضريبة القيمة المضافة", 0.05m, "AED"),
        Country.Bahrain => new("ضريبة القيمة المضافة", 0.10m, "BHD"),
        Country.Oman => new("ضريبة القيمة المضافة", 0.05m, "OMR"),
        Country.Qatar => new("ضريبة القيمة المضافة", 0m, "QAR"),
        Country.Kuwait => new("ضريبة القيمة المضافة", 0m, "KWD"),
        Country.UnitedKingdom => new("ضريبة القيمة المضافة (VAT)", 0.20m, "GBP"),
        // Guernsey has no VAT/GST; it uses the Guernsey pound, which is at par with (and billed as) sterling.
        Country.Guernsey => new("ضريبة القيمة المضافة", 0m, "GBP"),
        _ => throw new ArgumentOutOfRangeException(nameof(country), country, "دولة غير مدعومة.")
    };
}
