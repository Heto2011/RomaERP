using RomaERP.Domain.Common;

namespace RomaERP.Domain.Sales;

/// <summary>One collection follow-up with a customer (a call, a visit, a message, an email) and what they promised.</summary>
public class CustomerFollowUp : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime FollowUpDate { get; set; }
    /// <summary>Free label: call, visit, message, email.</summary>
    public string Method { get; set; } = string.Empty;
    public string? Note { get; set; }
    /// <summary>The date the customer promised to pay by, if they gave one.</summary>
    public DateTime? PromisedPayDate { get; set; }
}
