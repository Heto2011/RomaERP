using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Sales.DTOs;

public class PortalLinkStatusDto
{
    public bool HasActiveLink { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastViewedAtUtc { get; set; }
    public int ViewCount { get; set; }
}

public class CreatedPortalLinkDto
{
    /// <summary>The secret for the link. Shown once: only its hash is kept.</summary>
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public class PortalInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

/// <summary>What a customer sees through their link: their own invoices and statement, nothing else.</summary>
public class CustomerPortalDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<PortalInvoiceDto> Invoices { get; set; } = new();
    public PartnerStatementDto Statement { get; set; } = new();
}
