namespace RomaERP.Application.Accounting.DTOs;

public class ManualProfitEntryDto
{
    public Guid Id { get; set; }
    public int Dimension { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime PeriodMonth { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

public class CreateManualProfitEntryDto
{
    public int Dimension { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime PeriodMonth { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
}

public class UpdateManualProfitEntryDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime PeriodMonth { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
}
