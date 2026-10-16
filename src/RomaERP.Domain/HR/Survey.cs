using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public enum SurveyStatus
{
    Draft = 1,
    Open = 2,
    Closed = 3
}

public enum SurveyQuestionType
{
    /// <summary>1 (lowest) to 5 (highest).</summary>
    Rating = 1,
    YesNo = 2,
    Text = 3
}

public class Survey : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>When true, answers are stored without the employee's identity; only the fact that they took part is kept (to stop double answers).</summary>
    public bool IsAnonymous { get; set; } = true;
    public SurveyStatus Status { get; set; } = SurveyStatus.Draft;
}

public class SurveyQuestion : AuditableEntity
{
    public Guid SurveyId { get; set; }
    public Survey? Survey { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
    public SurveyQuestionType Type { get; set; }
}

public class SurveyResponse : AuditableEntity
{
    public Guid SurveyId { get; set; }
    public Survey? Survey { get; set; }
    /// <summary>Null for an anonymous survey.</summary>
    public Guid? EmployeeId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
}

public class SurveyAnswer : AuditableEntity
{
    public Guid SurveyResponseId { get; set; }
    public SurveyResponse? SurveyResponse { get; set; }
    public Guid SurveyQuestionId { get; set; }
    public SurveyQuestion? SurveyQuestion { get; set; }
    /// <summary>Rating 1–5, or 1 = yes / 0 = no.</summary>
    public int? NumberValue { get; set; }
    public string? TextValue { get; set; }
}

/// <summary>Records that an employee has answered a survey — kept apart from the answers so an anonymous survey stays anonymous.</summary>
public class SurveyParticipation : AuditableEntity
{
    public Guid SurveyId { get; set; }
    public Guid EmployeeId { get; set; }
}
