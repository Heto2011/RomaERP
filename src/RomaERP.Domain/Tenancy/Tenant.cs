using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>Registry row in the central database — points a company code at its own isolated database.</summary>
public class Tenant : AuditableEntity
{
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public Country Country { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
