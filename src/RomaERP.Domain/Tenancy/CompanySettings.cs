using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>Single-row settings living inside each tenant's own database (company profile, country, tax).</summary>
public class CompanySettings : AuditableEntity
{
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public Country Country { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public decimal VatRate { get; set; }
    public string DefaultCurrency { get; set; } = "EGP";
}
