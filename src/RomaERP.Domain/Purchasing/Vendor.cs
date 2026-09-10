using RomaERP.Domain.Common;

namespace RomaERP.Domain.Purchasing;

public class Vendor : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Running Accounts Payable balance for this vendor — increases on a Credit invoice,
    /// decreases as payments are recorded against it.</summary>
    public decimal ApBalance { get; set; }
}
