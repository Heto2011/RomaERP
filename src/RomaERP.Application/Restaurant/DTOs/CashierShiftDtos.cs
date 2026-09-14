namespace RomaERP.Application.Restaurant.DTOs;

public class CashierShiftDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public decimal OpeningFloat { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public decimal? ClosingCountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CashVariance { get; set; }
    public int Status { get; set; }
}

public class OpenCashierShiftDto
{
    public Guid EmployeeId { get; set; }
    public decimal OpeningFloat { get; set; }
}

public class CloseCashierShiftDto
{
    public decimal ClosingCountedCash { get; set; }
}
