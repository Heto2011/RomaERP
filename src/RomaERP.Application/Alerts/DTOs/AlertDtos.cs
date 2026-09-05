namespace RomaERP.Application.Alerts.DTOs;

public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

public class AlertDto
{
    public string Category { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

/// <summary>Pulls signals already computed by other real reports (inventory movement, cash flow intelligence,
/// waste analysis, item profitability, AR/AP aging) into one list, ranked by severity. Every alert here traces
/// back to a real number in an existing report — this raises no signal of its own. Thresholds (waste %, low
/// margin %, overdue AR/AP) are fixed defaults, not yet user-configurable.</summary>
public class AlertsReportDto
{
    public DateTime GeneratedAt { get; set; }
    public List<AlertDto> Alerts { get; set; } = new();
}
