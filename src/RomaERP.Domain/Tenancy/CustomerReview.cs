using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

public enum ReviewStatus { Pending = 0, Approved = 1, Rejected = 2 }

/// <summary>What a real customer said about the product, asked for at the end of the month. It is only ever shown on the website when the
/// customer agreed to publish it AND we approved it: nothing is invented, edited or shown without both.</summary>
public class CustomerReview : BaseEntity
{
    public Guid TenantId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    /// <summary>How the customer wants to be named on the website (the first name by default).</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>1 to 5.</summary>
    public int Rating { get; set; }
    public string? Comment { get; set; }
    /// <summary>Which product it is about: ERP, HR, POS or Books.</summary>
    public string Product { get; set; } = "ERP";
    /// <summary>The customer agreed that the review may appear on the website.</summary>
    public bool AllowPublish { get; set; }
    /// <summary>The customer agreed that the company name may appear next to it.</summary>
    public bool ShowCompany { get; set; }
    /// <summary>yyyy-MM of the month the review answers; one per person per month.</summary>
    public string PeriodKey { get; set; } = string.Empty;
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; set; }
    public string? AdminNote { get; set; }
}
