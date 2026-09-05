using RomaERP.Application.Restaurant.DTOs;

namespace RomaERP.Application.Restaurant.Services;

public interface IDeliveryReconciliationService
{
    Task<DeliverySettlementImportDto> ImportAsync(Stream csvStream, string fileName, string platformName, string userId, CancellationToken ct = default);
    Task<List<DeliverySettlementImportDto>> GetImportsAsync(CancellationToken ct = default);
    Task<DeliveryReconciliationReportDto> GetReconciliationAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}
