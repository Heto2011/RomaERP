using RomaERP.Domain.Common;
using RomaERP.Domain.HR;

namespace RomaERP.Domain.Restaurant;

public enum CashierShiftStatus
{
    Open = 1,
    Closed = 2
}

/// <summary>A cashier's working session at the POS: opens with a counted cash float, sells against it, then
/// closes by counting the drawer again. ExpectedCash/CashVariance are computed at close time from the real
/// cash-settled orders billed under this shift — never entered manually.</summary>
public class CashierShift : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime OpenedAtUtc { get; set; }
    public decimal OpeningFloat { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public decimal? ClosingCountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CashVariance { get; set; }

    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;
}
