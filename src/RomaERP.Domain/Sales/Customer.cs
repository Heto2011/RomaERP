using RomaERP.Domain.Common;

namespace RomaERP.Domain.Sales;

public class Customer : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Running Accounts Receivable balance for this customer — increases on a Credit invoice,
    /// decreases as payments are recorded against it. Same pattern as Employee.CustodyBalance.</summary>
    public decimal ArBalance { get; set; }
}
