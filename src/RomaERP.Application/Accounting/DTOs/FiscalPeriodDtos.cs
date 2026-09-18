namespace RomaERP.Application.Accounting.DTOs;

public class FiscalYearDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public List<FiscalPeriodDto> Periods { get; set; } = new();
}

public class FiscalPeriodDto
{
    public Guid Id { get; set; }
    public Guid FiscalYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PeriodNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}
