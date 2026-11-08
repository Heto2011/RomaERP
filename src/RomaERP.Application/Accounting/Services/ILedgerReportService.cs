using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface ILedgerReportService
{
    Task<GeneralLedgerDto> GetGeneralLedgerAsync(Guid accountId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<PartnerStatementDto> GetCustomerStatementAsync(Guid customerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<PartnerStatementDto> GetVendorStatementAsync(Guid vendorId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}
