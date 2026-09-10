namespace RomaERP.Application.Accounting.DTOs;

public class OpeningBalanceLineInputDto
{
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class CreateOpeningBalanceDto
{
    public Guid FiscalPeriodId { get; set; }
    public DateTime EntryDate { get; set; }
    public List<OpeningBalanceLineInputDto> Lines { get; set; } = new();
}
