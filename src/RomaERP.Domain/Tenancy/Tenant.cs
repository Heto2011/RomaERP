using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>Which product a tenant signed up for — controls what the frontend shows them after login, not
/// a hard data boundary (a PeopleOnly tenant's data is exactly as isolated as any other tenant's, since
/// isolation comes from the database-per-tenant design, not from this flag).</summary>
public enum ProductScope
{
    /// <summary>The full RomaERP suite — accounting, inventory, sales, HR, POS, everything.</summary>
    Full = 1,

    /// <summary>Signed up for ROMA People only — lands straight in the HR portal after login and the main
    /// app's sidebar only shows the sections an HR-only customer needs (general, HR, user administration).</summary>
    PeopleOnly = 2
}

/// <summary>Registry row in the central database — points a company code at its own isolated database.</summary>
public class Tenant : AuditableEntity
{
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public Country Country { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ProductScope ProductScope { get; set; } = ProductScope.Full;

    /// <summary>Marks a tenant created for a sales demo rather than a real customer, so it can be tracked
    /// and later deactivated separately from paying tenants.</summary>
    public bool IsDemo { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
