using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public enum JobStatus
{
    Draft = 1,
    Open = 2,
    Closed = 3
}

public enum CandidateStage
{
    Applied = 1,
    Screening = 2,
    Interview = 3,
    Offer = 4,
    Hired = 5,
    Rejected = 6
}

/// <summary>An open (or planned) position. Open jobs appear on the company's public careers page.</summary>
public class JobPosting : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public DateTime? ClosingDate { get; set; }
}

public class Candidate : AuditableEntity
{
    public Guid JobPostingId { get; set; }
    public JobPosting? JobPosting { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    /// <summary>The CV pasted as text — this is what the AI screening reads.</summary>
    public string ResumeText { get; set; } = string.Empty;
    public string? CoverNote { get; set; }
    /// <summary>"Careers page" for self-applied candidates, "Manual" for ones HR typed in.</summary>
    public string Source { get; set; } = "Manual";

    public CandidateStage Stage { get; set; } = CandidateStage.Applied;
    public string? Notes { get; set; }
    public DateTime? InterviewAtUtc { get; set; }

    /// <summary>0–100 fit with the job, from the AI screening. Null until screened. A suggestion for HR, never a decision.</summary>
    public int? AiScore { get; set; }
    public string? AiSummary { get; set; }
    public DateTime? AiScreenedAtUtc { get; set; }
}
