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

    // ----- Full details -----
    public string? ContactPerson { get; set; }
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? CommercialRegisterNumber { get; set; }
    public string? Notes { get; set; }
    /// <summary>Days of credit agreed (0 = pay on the spot).</summary>
    public int PaymentTermsDays { get; set; }
    /// <summary>Informational ceiling for the open balance; shown on the card, not yet enforced when invoicing.</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>Running Accounts Receivable balance for this customer — increases on a Credit invoice,
    /// decreases as payments are recorded against it. Same pattern as Employee.CustodyBalance.</summary>
    public decimal ArBalance { get; set; }
}
