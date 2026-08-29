using RomaERP.Application.Inventory.DTOs;

namespace RomaERP.Application.Inventory.Services;

public interface IInventoryReportService
{
    Task<StockValuationReportDto> GetStockValuationAsync(CancellationToken ct = default);
    Task<InventoryMovementReportDto> GetInventoryMovementAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<PurchasePriceVarianceReportDto> GetPurchasePriceVarianceAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<RecipeCostReportDto> GetRecipeCostAsync(CancellationToken ct = default);
}
