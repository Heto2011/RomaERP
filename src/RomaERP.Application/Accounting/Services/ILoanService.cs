using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface ILoanService
{
    Task<List<LoanDto>> GetAllAsync(CancellationToken ct = default);
    Task<LoanDto> CreateAsync(CreateLoanDto dto, CancellationToken ct = default);
    /// <summary>Pays off the whole remaining principal at once (plus any early-settlement charge), closing the loan.</summary>
    Task<LoanDto> SettleAsync(Guid loanId, SettleLoanDto dto, CancellationToken ct = default);
    Task<LoanDto> PayInstallmentAsync(Guid installmentId, PayInstallmentDto dto, CancellationToken ct = default);

    /// <summary>Equal monthly payments (a level annuity). The last installment absorbs the rounding so the principal comes to exactly zero.</summary>
    List<LoanInstallmentDto> BuildSchedule(decimal principal, decimal annualPercent, int months, DateTime firstDueDate);
}
