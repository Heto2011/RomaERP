using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public enum ReviewCycleStatus
{
    Draft = 1,
    Active = 2,
    Closed = 3
}

public enum ReviewRelationship
{
    Self = 1,
    Manager = 2,
    Peer = 3,
    DirectReport = 4
}

/// <summary>A round of performance reviews (e.g. "H2 2026"): a fixed list of competencies everyone is rated on, 1 to 5.</summary>
public class ReviewCycle : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ReviewCycleStatus Status { get; set; } = ReviewCycleStatus.Draft;
    /// <summary>One competency per line.</summary>
    public string Competencies { get; set; } = string.Empty;
}

/// <summary>One reviewer's form about one employee. A 360 review is simply several of these for the same subject with different relationships.</summary>
public class ReviewAssignment : AuditableEntity
{
    public Guid ReviewCycleId { get; set; }
    public ReviewCycle? ReviewCycle { get; set; }

    public Guid SubjectEmployeeId { get; set; }
    public Employee? SubjectEmployee { get; set; }

    public Guid ReviewerEmployeeId { get; set; }
    public Employee? ReviewerEmployee { get; set; }

    public ReviewRelationship Relationship { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    /// <summary>Average of the competency ratings, computed when the form is submitted.</summary>
    public decimal? OverallRating { get; set; }
    /// <summary>JSON object: competency name → rating (1–5).</summary>
    public string? RatingsJson { get; set; }
    public string? Comments { get; set; }
}
