using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

public class FiscalYear : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }

    public ICollection<FiscalPeriod> Periods { get; set; } = new List<FiscalPeriod>();
}

public class FiscalPeriod : AuditableEntity
{
    public Guid FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }

    public string Name { get; set; } = string.Empty;
    public int PeriodNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}
