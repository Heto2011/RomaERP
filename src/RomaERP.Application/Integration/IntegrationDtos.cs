using RomaERP.Domain.Common;

namespace RomaERP.Application.Integration;

/// <summary>What the bot sends for a sales invoice. Customers, items and warehouses are named by their codes, never by ids.</summary>
public class IntegrationSalesInvoicePayload
{
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.Credit;
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public string? WarehouseCode { get; set; }
    public int? NumberOfInstallments { get; set; }
    public DateTime? FirstInstallmentDueDate { get; set; }
    public List<IntegrationSalesLine> Lines { get; set; } = new();
}

public class IntegrationSalesLine
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? ItemCode { get; set; }
}

public class IntegrationPurchaseInvoicePayload
{
    public string? VendorCode { get; set; }
    public string? VendorName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.Credit;
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public string? ReceiveToWarehouseCode { get; set; }
    public List<IntegrationPurchaseLine> Lines { get; set; } = new();
}

public class IntegrationPurchaseLine
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    /// <summary>The account the cost goes to. Defaults to general expenses (5300), or inventory (1160) when an item is given.</summary>
    public string? AccountCode { get; set; }
    public string? ItemCode { get; set; }
}

/// <summary>A payment received from a customer or made to a vendor, tied to an invoice the bot sent earlier (by the bot's own reference).</summary>
public class IntegrationPaymentPayload
{
    public string InvoiceExternalRef { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentTerm Method { get; set; } = PaymentTerm.Cash;
    public DateTime PaymentDate { get; set; }
    public string? Reference { get; set; }
}

public class IntegrationExpensePayload
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Defaults to general and administrative expenses (5300).</summary>
    public string? ExpenseAccountCode { get; set; }
    /// <summary>"cash" (default), "bank" or "payable"; or give PaidFromAccountCode for any other account.</summary>
    public string? PaidFrom { get; set; }
    public string? PaidFromAccountCode { get; set; }
    public string? Reference { get; set; }
}

public class IntegrationJournalPayload
{
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public List<IntegrationJournalLine> Lines { get; set; } = new();
}

public class IntegrationJournalLine
{
    public string AccountCode { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

public class IntegrationReceiveResult
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>True when this (type, reference) was already received: nothing new was created.</summary>
    public bool Duplicate { get; set; }
}

public class InboxItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ExternalRef { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? ResultEntityId { get; set; }
    /// <summary>Only filled when one item is opened.</summary>
    public string? PayloadJson { get; set; }
}

public class ApproveAllResult
{
    public int Approved { get; set; }
    public int Failed { get; set; }
}

public class ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
}

public class CreatedApiKeyDto : ApiKeyDto
{
    /// <summary>The full key. Shown once; it cannot be read again.</summary>
    public string Key { get; set; } = string.Empty;
}

public class IntegrationItemPayload
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? UnitOfMeasure { get; set; }
    /// <summary>The item category's code. Empty means the first active category (or a general one is made).</summary>
    public string? CategoryCode { get; set; }
    public decimal ReorderLevel { get; set; }
    public int LeadTimeDays { get; set; } = 2;
    public bool IsLotTracked { get; set; }
}

public class IntegrationBankLinePayload
{
    /// <summary>The bank account's code in the chart of accounts. Defaults to 1112.</summary>
    public string? AccountCode { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Positive = money in, negative = money out.</summary>
    public decimal Amount { get; set; }
}
